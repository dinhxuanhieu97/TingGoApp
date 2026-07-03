using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Ordering.Domain;
using TingGo.Modules.Ordering.Services;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Ordering.Endpoints;

public sealed record TransitionDto(long RowVersion, string? Reason);

public static class MerchantOrderEndpoints
{
    private static readonly string[] ActiveStatuses =
        [OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/orders", async (
            Guid venueId, string? status, int? limit, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, OrderService orders, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var query = db.Set<Order>().AsNoTracking().Where(x => x.VenueId == venueId);
            if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
            var list = await query.OrderByDescending(x => x.PlacedAt).Take(Math.Clamp(limit ?? 50, 1, 200)).ToListAsync(ct);
            return Results.Ok(await ToViewsWithTableAsync(db, orders, list, ct));
        }).RequireAuthorization();

        // Reconnect snapshot (MOB-06): order đang hoạt động
        endpoints.MapGet("/venues/{venueId:guid}/orders/active", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, OrderService orders, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var list = await db.Set<Order>().AsNoTracking()
                .Where(x => x.VenueId == venueId && ActiveStatuses.Contains(x.Status))
                .OrderBy(x => x.PlacedAt)
                .ToListAsync(ct);
            return Results.Ok(await ToViewsWithTableAsync(db, orders, list, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/orders/{orderId:guid}", async (
            Guid orderId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, OrderService orders, CancellationToken ct) =>
        {
            var order = await LoadOrderAsync(db, orderId, ct);
            await EnsureStaffAsync(venues, memberships, principal, order.VenueId, ct);
            return Results.Ok(await orders.ToViewAsync(order, ct));
        }).RequireAuthorization();

        endpoints.MapGet("/orders/{orderId:guid}/history", async (
            Guid orderId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var order = await LoadOrderAsync(db, orderId, ct);
            await EnsureStaffAsync(venues, memberships, principal, order.VenueId, ct);
            var history = await db.Set<OrderStatusHistory>().AsNoTracking()
                .Where(x => x.OrderId == orderId).OrderBy(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(history);
        }).RequireAuthorization();

        MapTransition(endpoints, "confirm", OrderStatus.Confirmed, requireManager: false);
        MapTransition(endpoints, "reject", OrderStatus.Rejected, requireManager: false);
        MapTransition(endpoints, "start-preparing", OrderStatus.Preparing, requireManager: false);
        MapTransition(endpoints, "mark-ready", OrderStatus.Ready, requireManager: false);
        MapTransition(endpoints, "complete", OrderStatus.Completed, requireManager: false);
        MapTransition(endpoints, "cancel", OrderStatus.Cancelled, requireManager: true); // Hủy theo quyền (MER-05)
    }

    private static void MapTransition(
        IEndpointRouteBuilder endpoints, string action, string targetStatus, bool requireManager)
    {
        endpoints.MapPost($"/orders/{{orderId:guid}}/{action}", async (
            Guid orderId, TransitionDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, OrderService orders, CancellationToken ct) =>
        {
            var order = await LoadOrderAsync(db, orderId, ct);
            var membership = await EnsureStaffAsync(venues, memberships, principal, order.VenueId, ct);
            if (requireManager && membership.Role is not ("owner" or "manager"))
            {
                throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được hủy order.", 403);
            }

            if (order.RowVersion != dto.RowVersion)
            {
                throw new ApiException(ErrorCodes.OrderStaleVersion,
                    "Order đã được cập nhật bởi người khác. Hãy tải lại.", 409,
                    new Dictionary<string, object?> { ["currentRowVersion"] = order.RowVersion });
            }

            var history = OrderStateMachine.Transition(order, targetStatus, membership.MembershipId, dto.Reason);
            db.Add(history);
            db.Add(orders.BuildOutboxEvent(order, $"order.{TargetEventName(targetStatus)}"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(await orders.ToViewAsync(order, ct));
        }).RequireAuthorization();
    }

    private static string TargetEventName(string status) => status switch
    {
        OrderStatus.Confirmed => "confirmed",
        OrderStatus.Rejected => "rejected",
        OrderStatus.Preparing => "preparing",
        OrderStatus.Ready => "ready",
        OrderStatus.Completed => "completed",
        OrderStatus.Cancelled => "cancelled",
        _ => status,
    };

    private static async Task<Order> LoadOrderAsync(TingGoDbContext db, Guid orderId, CancellationToken ct)
        => await db.Set<Order>().FirstOrDefaultAsync(x => x.Id == orderId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy order.", 404);

    private static async Task<(Guid MembershipId, string Role)> EnsureStaffAsync(
        IVenueDirectory venues, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var organizationId = await venues.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return await memberships.GetMembershipAsync(Guid.Parse(sub), organizationId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
    }

    private static async Task<List<object>> ToViewsWithTableAsync(
        TingGoDbContext db, OrderService orders, List<Order> list, CancellationToken ct)
    {
        var sessionIds = list.Select(x => x.TableSessionId).Distinct().ToList();
        var sessions = await db.Set<TableSession>().AsNoTracking()
            .Where(x => sessionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TableId })
            .ToDictionaryAsync(x => x.Id, ct);

        var views = new List<object>();
        foreach (var order in list)
        {
            var view = await orders.ToViewAsync(order, ct);
            views.Add(new { view, tableId = sessions.GetValueOrDefault(order.TableSessionId)?.TableId });
        }
        return views;
    }
}
