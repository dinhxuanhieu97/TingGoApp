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

public sealed record CreateSessionDto(string QrToken);

public static class PublicOrderEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Tạo hoặc lấy phiên bàn đang mở (PRD 7.8)
        endpoints.MapPost("/public/table-sessions", async (
            CreateSessionDto dto, TingGoDbContext db, IVenueDirectory venues,
            SessionTokenService tokens, CancellationToken ct) =>
        {
            var table = await venues.GetActiveTableByQrTokenAsync(dto.QrToken, ct)
                ?? throw new ApiException(ErrorCodes.QrRevoked,
                    "Mã QR không hợp lệ hoặc đã bị thu hồi.", 410);

            var session = await db.Set<TableSession>()
                .FirstOrDefaultAsync(x => x.TableId == table.TableId
                                          && x.Status != TableSessionStatus.Closed, ct);
            if (session is null)
            {
                session = new TableSession { VenueId = table.VenueId, TableId = table.TableId };
                session.PublicTokenHash = tokens.HashOf(tokens.TokenFor(session.Id));
                db.Add(session);
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new
            {
                sessionToken = tokens.TokenFor(session.Id),
                session.Status,
                session.OpenedAt,
                table = new { table.TableId, table.Code, table.Name },
            });
        });

        // Xem phiên + danh sách order (CUS-06/07: theo dõi trạng thái, gọi thêm món)
        endpoints.MapGet("/public/table-sessions/{token}/orders", async (
            string token, TingGoDbContext db, OrderService orders, CancellationToken ct) =>
        {
            var session = await orders.GetOpenSessionAsync(token, ct);
            var orderEntities = await db.Set<Order>().AsNoTracking()
                .Where(x => x.TableSessionId == session.Id)
                .OrderByDescending(x => x.PlacedAt)
                .ToListAsync(ct);
            var views = new List<OrderView>();
            foreach (var order in orderEntities)
            {
                views.Add(await orders.ToViewAsync(order, ct));
            }
            var total = orderEntities
                .Where(x => x.Status is not (OrderStatus.Rejected or OrderStatus.Cancelled))
                .Sum(x => x.TotalMinor);
            return Results.Ok(new { session.Status, totalMinor = total, orders = views });
        });

        // Gửi order (CUS-05) — bắt buộc Idempotency-Key
        endpoints.MapPost("/public/orders", async (
            HttpRequest request, SubmitOrderDto dto, OrderService orders, CancellationToken ct) =>
        {
            var idempotencyKey = request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Thiếu header Idempotency-Key.", 400);
            }

            var (statusCode, json) = await orders.SubmitOrderAsync(idempotencyKey, dto, ct);
            return Results.Content(json, "application/json", statusCode: statusCode);
        });
    }
}
