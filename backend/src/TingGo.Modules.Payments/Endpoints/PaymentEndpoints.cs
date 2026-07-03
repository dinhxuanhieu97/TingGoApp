using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Payments.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Payments.Endpoints;

public sealed record CreatePaymentDto(string Method, long? AmountMinor);

public static class PaymentEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Tạo thanh toán cho phiên bàn — mặc định bằng bill còn lại chưa thu
        endpoints.MapPost("/table-sessions/{sessionId:guid}/payments", async (
            Guid sessionId, CreatePaymentDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            ITableSessionReader sessions, IVenueDirectory venues, IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!PaymentMethod.All.Contains(dto.Method))
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    $"Method phải là: {string.Join(", ", PaymentMethod.All)}.", 400);
            }

            var bill = await sessions.GetSessionBillAsync(sessionId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy phiên bàn.", 404);
            await EnsureStaffAsync(venues, memberships, principal, bill.VenueId, ct);

            var alreadyPaid = await db.Set<Payment>()
                .Where(x => x.TableSessionId == sessionId && x.Status == PaymentStatus.Paid)
                .SumAsync(x => (long?)x.AmountMinor, ct) ?? 0;
            var remaining = bill.TotalMinor - alreadyPaid;
            var amount = dto.AmountMinor ?? remaining;
            if (amount <= 0 || amount > remaining)
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    $"Số tiền không hợp lệ. Còn phải thu: {remaining}.", 400,
                    new Dictionary<string, object?> { ["remainingMinor"] = remaining });
            }

            var payment = new Payment
            {
                VenueId = bill.VenueId,
                TableSessionId = sessionId,
                Method = dto.Method,
                AmountMinor = amount,
                CurrencyCode = bill.CurrencyCode,
            };
            db.Add(payment);
            db.Add(BuildEvent(payment, "payment.created"));
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/payments/{payment.Id}", payment);
        }).RequireAuthorization();

        endpoints.MapGet("/payments/{paymentId:guid}", async (
            Guid paymentId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var payment = await LoadAsync(db, paymentId, ct);
            await EnsureStaffAsync(venues, memberships, principal, payment.VenueId, ct);
            return Results.Ok(payment);
        }).RequireAuthorization();

        // Xác nhận đã thu (cash hoặc đối chiếu chuyển khoản thủ công — ADR-004)
        endpoints.MapPost("/payments/{paymentId:guid}/confirm-cash", async (
            Guid paymentId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var payment = await LoadAsync(db, paymentId, ct);
            var membership = await EnsureStaffAsync(venues, memberships, principal, payment.VenueId, ct);
            if (payment.Status != PaymentStatus.Pending)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    $"Thanh toán đang ở trạng thái '{payment.Status}'.", 409);
            }
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTimeOffset.UtcNow;
            db.Add(BuildEvent(payment, "payment.paid"));
            db.Add(new AuditLog
            {
                VenueId = payment.VenueId,
                ActorUserId = membership.MembershipId,
                Action = "payment.confirm",
                EntityType = "Payment",
                EntityId = payment.Id,
                Detail = $"{payment.Method} {payment.AmountMinor} {payment.CurrencyCode}",
            });
            await db.SaveChangesAsync(ct);
            return Results.Ok(payment);
        }).RequireAuthorization();

        endpoints.MapPost("/payments/{paymentId:guid}/cancel", async (
            Guid paymentId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var payment = await LoadAsync(db, paymentId, ct);
            await EnsureStaffAsync(venues, memberships, principal, payment.VenueId, ct);
            if (payment.Status != PaymentStatus.Pending)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    $"Chỉ hủy được thanh toán đang chờ (hiện: '{payment.Status}').", 409);
            }
            payment.Status = PaymentStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            return Results.Ok(payment);
        }).RequireAuthorization();

        // Danh sách thanh toán của phiên (hiển thị khi đóng bàn)
        endpoints.MapGet("/table-sessions/{sessionId:guid}/payments", async (
            Guid sessionId, ClaimsPrincipal principal, TingGoDbContext db,
            ITableSessionReader sessions, IVenueDirectory venues, IMembershipService memberships,
            CancellationToken ct) =>
        {
            var bill = await sessions.GetSessionBillAsync(sessionId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy phiên bàn.", 404);
            await EnsureStaffAsync(venues, memberships, principal, bill.VenueId, ct);
            var payments = await db.Set<Payment>().AsNoTracking()
                .Where(x => x.TableSessionId == sessionId)
                .OrderBy(x => x.CreatedAt).ToListAsync(ct);
            var paid = payments.Where(x => x.Status == PaymentStatus.Paid).Sum(x => x.AmountMinor);
            return Results.Ok(new
            {
                billTotalMinor = bill.TotalMinor,
                paidMinor = paid,
                remainingMinor = bill.TotalMinor - paid,
                payments,
            });
        }).RequireAuthorization();
    }

    private static async Task<Payment> LoadAsync(TingGoDbContext db, Guid paymentId, CancellationToken ct)
        => await db.Set<Payment>().FirstOrDefaultAsync(x => x.Id == paymentId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy thanh toán.", 404);

    private static OutboxEvent BuildEvent(Payment payment, string eventType)
        => new()
        {
            VenueId = payment.VenueId,
            AggregateType = "Payment",
            AggregateId = payment.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new
            {
                eventId = Guid.CreateVersion7(),
                eventType,
                occurredAt = DateTimeOffset.UtcNow,
                venueId = payment.VenueId,
                entityId = payment.Id,
                version = 1,
                data = new
                {
                    payment.Id, payment.Method, payment.Status, payment.AmountMinor,
                    payment.CurrencyCode, tableSessionId = payment.TableSessionId,
                },
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };

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
}
