using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Ordering.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Ordering.Endpoints;

public sealed record SessionActionDto(long RowVersion);

/// <summary>MER-06: quản lý phiên bàn — bill, đóng, mở lại.</summary>
public static class SessionEndpoints
{
    private static readonly string[] BillableStatuses =
        [OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed];

    private static readonly string[] UnfinishedStatuses =
        [OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/table-sessions", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await ServiceRequestEndpoints.EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var sessions = await db.Set<TableSession>().AsNoTracking()
                .Where(x => x.VenueId == venueId && x.Status != TableSessionStatus.Closed)
                .Select(s => new
                {
                    s.Id, s.TableId, s.Status, s.OpenedAt, s.RowVersion,
                    orderCount = db.Set<Order>().Count(o => o.TableSessionId == s.Id),
                    totalMinor = db.Set<Order>()
                        .Where(o => o.TableSessionId == s.Id && BillableStatuses.Contains(o.Status))
                        .Sum(o => (long?)o.TotalMinor) ?? 0,
                })
                .ToListAsync(ct);
            return Results.Ok(sessions);
        }).RequireAuthorization();

        endpoints.MapGet("/table-sessions/{sessionId:guid}/bill", async (
            Guid sessionId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var session = await LoadSessionAsync(db, sessionId, ct);
            await ServiceRequestEndpoints.EnsureStaffAsync(venues, memberships, principal, session.VenueId, ct);

            var orders = await db.Set<Order>().AsNoTracking()
                .Where(o => o.TableSessionId == sessionId)
                .OrderBy(o => o.PlacedAt)
                .Select(o => new { o.Id, o.OrderNumber, o.Status, o.TotalMinor, o.CurrencyCode, o.PlacedAt })
                .ToListAsync(ct);
            var billable = orders.Where(o => BillableStatuses.Contains(o.Status)).ToList();
            return Results.Ok(new
            {
                session.Id, session.TableId, session.Status, session.OpenedAt, session.RowVersion,
                orders,
                totalMinor = billable.Sum(o => o.TotalMinor),
                currencyCode = billable.FirstOrDefault()?.CurrencyCode ?? "VND",
            });
        }).RequireAuthorization();

        endpoints.MapPost("/table-sessions/{sessionId:guid}/close", async (
            Guid sessionId, SessionActionDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var session = await LoadSessionAsync(db, sessionId, ct);
            var membership = await ServiceRequestEndpoints.EnsureStaffAsync(
                venues, memberships, principal, session.VenueId, ct);
            CheckRowVersion(session, dto.RowVersion);

            var unfinished = await db.Set<Order>()
                .CountAsync(o => o.TableSessionId == sessionId && UnfinishedStatuses.Contains(o.Status), ct);
            if (unfinished > 0)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    $"Còn {unfinished} order chưa hoàn thành. Hãy hoàn thành hoặc hủy trước khi đóng bàn.", 409);
            }

            session.Status = TableSessionStatus.Closed;
            session.ClosedAt = DateTimeOffset.UtcNow;
            session.RowVersion++;
            db.Add(BuildSessionEvent(session));
            db.Add(Audit(session, membership.MembershipId, "table_session.close"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { session.Id, session.Status, session.ClosedAt, session.RowVersion });
        }).RequireAuthorization();

        endpoints.MapPost("/table-sessions/{sessionId:guid}/reopen", async (
            Guid sessionId, SessionActionDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var session = await LoadSessionAsync(db, sessionId, ct);
            var membership = await ServiceRequestEndpoints.EnsureStaffAsync(
                venues, memberships, principal, session.VenueId, ct);
            // Mở lại bàn theo quyền quản lý (MER-06)
            if (membership.Role is not ("owner" or "manager"))
            {
                throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được mở lại bàn.", 403);
            }
            CheckRowVersion(session, dto.RowVersion);

            session.Status = TableSessionStatus.Open;
            session.ClosedAt = null;
            session.RowVersion++;
            db.Add(BuildSessionEvent(session));
            db.Add(Audit(session, membership.MembershipId, "table_session.reopen"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { session.Id, session.Status, session.RowVersion });
        }).RequireAuthorization();
    }

    private static void CheckRowVersion(TableSession session, long rowVersion)
    {
        if (session.RowVersion != rowVersion)
        {
            throw new ApiException(ErrorCodes.Conflict,
                "Phiên bàn đã thay đổi. Hãy tải lại.", 409,
                new Dictionary<string, object?> { ["currentRowVersion"] = session.RowVersion });
        }
    }

    private static async Task<TableSession> LoadSessionAsync(TingGoDbContext db, Guid sessionId, CancellationToken ct)
        => await db.Set<TableSession>().FirstOrDefaultAsync(x => x.Id == sessionId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy phiên bàn.", 404);

    private static OutboxEvent BuildSessionEvent(TableSession session)
        => new()
        {
            VenueId = session.VenueId,
            AggregateType = "TableSession",
            AggregateId = session.Id,
            EventType = "table_session.updated",
            Payload = JsonSerializer.Serialize(new
            {
                eventId = Guid.CreateVersion7(),
                eventType = "table_session.updated",
                occurredAt = DateTimeOffset.UtcNow,
                venueId = session.VenueId,
                entityId = session.Id,
                version = session.RowVersion,
                data = new { session.Id, session.TableId, session.Status, tableSessionId = session.Id },
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };

    private static AuditLog Audit(TableSession session, Guid membershipId, string action)
        => new()
        {
            VenueId = session.VenueId,
            ActorUserId = membershipId,
            Action = action,
            EntityType = "TableSession",
            EntityId = session.Id,
        };
}
