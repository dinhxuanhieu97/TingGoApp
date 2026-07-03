using System.Security.Claims;
using System.Text.Json;
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

public sealed record CreateServiceRequestDto(string SessionToken, string Type, string? Note);

public static class ServiceRequestEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // CUS-08: khách gọi nhân viên (không auth)
        endpoints.MapPost("/public/service-requests", async (
            CreateServiceRequestDto dto, TingGoDbContext db, OrderService orders, CancellationToken ct) =>
        {
            if (!ServiceRequestType.All.Contains(dto.Type))
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    $"Type phải là: {string.Join(", ", ServiceRequestType.All)}.", 400);
            }
            var session = await orders.GetOpenSessionAsync(dto.SessionToken, ct);

            // Chống spam: tối đa 3 request pending mỗi phiên
            var pendingCount = await db.Set<ServiceRequest>()
                .CountAsync(x => x.TableSessionId == session.Id && x.Status == ServiceRequestStatus.Pending, ct);
            if (pendingCount >= 3)
            {
                throw new ApiException(ErrorCodes.RateLimited,
                    "Đã có yêu cầu đang chờ. Nhân viên sẽ đến ngay.", 429);
            }

            var request = new ServiceRequest
            {
                VenueId = session.VenueId,
                TableSessionId = session.Id,
                Type = dto.Type,
                Note = dto.Note?.Length > 200 ? dto.Note[..200] : dto.Note,
            };
            db.Add(request);
            db.Add(BuildEvent(request, "service_request.created"));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/public/service-requests/{request.Id}",
                new { request.Id, request.Type, request.Status, request.RequestedAt });
        });

        endpoints.MapGet("/venues/{venueId:guid}/service-requests", async (
            Guid venueId, string? status, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var query = db.Set<ServiceRequest>().AsNoTracking().Where(x => x.VenueId == venueId);
            query = status is null
                ? query.Where(x => x.Status == ServiceRequestStatus.Pending
                                   || x.Status == ServiceRequestStatus.Acknowledged)
                : query.Where(x => x.Status == status);

            var items = await (
                from request in query
                join session in db.Set<TableSession>().AsNoTracking() on request.TableSessionId equals session.Id
                orderby request.RequestedAt
                select new { request.Id, request.Type, request.Note, request.Status, request.RequestedAt, session.TableId }
            ).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        MapAction(endpoints, "acknowledge", ServiceRequestStatus.Acknowledged,
            from: [ServiceRequestStatus.Pending]);
        MapAction(endpoints, "resolve", ServiceRequestStatus.Resolved,
            from: [ServiceRequestStatus.Pending, ServiceRequestStatus.Acknowledged]);
        MapAction(endpoints, "cancel", ServiceRequestStatus.Cancelled,
            from: [ServiceRequestStatus.Pending, ServiceRequestStatus.Acknowledged]);
    }

    private static void MapAction(
        IEndpointRouteBuilder endpoints, string action, string target, string[] from)
    {
        endpoints.MapPost($"/service-requests/{{id:guid}}/{action}", async (
            Guid id, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var request = await db.Set<ServiceRequest>().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy yêu cầu.", 404);
            await EnsureStaffAsync(venues, memberships, principal, request.VenueId, ct);

            if (!from.Contains(request.Status))
            {
                throw new ApiException(ErrorCodes.Conflict,
                    $"Yêu cầu đang ở trạng thái '{request.Status}', không thể {action}.", 409);
            }
            request.Status = target;
            if (target is ServiceRequestStatus.Resolved or ServiceRequestStatus.Cancelled)
            {
                request.ResolvedAt = DateTimeOffset.UtcNow;
            }
            db.Add(BuildEvent(request, $"service_request.{target}"));
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { request.Id, request.Status, request.ResolvedAt });
        }).RequireAuthorization();
    }

    internal static OutboxEvent BuildEvent(ServiceRequest request, string eventType)
        => new()
        {
            VenueId = request.VenueId,
            AggregateType = "ServiceRequest",
            AggregateId = request.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new
            {
                eventId = Guid.CreateVersion7(),
                eventType,
                occurredAt = DateTimeOffset.UtcNow,
                venueId = request.VenueId,
                entityId = request.Id,
                version = 1,
                data = new { request.Id, request.Type, request.Note, request.Status, tableSessionId = request.TableSessionId },
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };

    internal static async Task<(Guid MembershipId, string Role)> EnsureStaffAsync(
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
}
