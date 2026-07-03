using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Ordering.Domain;
using TingGo.Modules.Payments.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Endpoints;

/// <summary>
/// MER-08 / PRD 7.14 — báo cáo cơ bản. Đặt ở host vì đọc chéo Ordering + Payments (read-only).
/// "Hôm nay" tính theo timezone của venue (PRD 5.4 — không hard-code múi giờ VN).
/// </summary>
public static class ReportEndpoints
{
    private static readonly string[] CountedStatuses =
        [OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed];

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/reports/today", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var venueInfo = await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var (dayStart, dayEnd, localDate) = TodayRange(venueInfo.Timezone);

            var orders = await db.Set<Order>().AsNoTracking()
                .Where(o => o.VenueId == venueId && o.PlacedAt >= dayStart && o.PlacedAt < dayEnd)
                .Select(o => new { o.Status, o.TotalMinor })
                .ToListAsync(ct);
            var payments = await db.Set<Payment>().AsNoTracking()
                .Where(p => p.VenueId == venueId && p.Status == PaymentStatus.Paid
                            && p.PaidAt >= dayStart && p.PaidAt < dayEnd)
                .Select(p => new { p.Method, p.AmountMinor })
                .ToListAsync(ct);

            var counted = orders.Where(o => CountedStatuses.Contains(o.Status)).ToList();
            return Results.Ok(new
            {
                date = localDate.ToString("yyyy-MM-dd"),
                timezone = venueInfo.Timezone,
                currencyCode = venueInfo.CurrencyCode,
                revenuePaidMinor = payments.Sum(p => p.AmountMinor),
                orderCount = counted.Count,
                orderTotalMinor = counted.Sum(o => o.TotalMinor),
                averageOrderMinor = counted.Count == 0 ? 0 : counted.Sum(o => o.TotalMinor) / counted.Count,
                rejectedOrCancelled = orders.Count - counted.Count,
                byPaymentMethod = payments.GroupBy(p => p.Method)
                    .Select(g => new { method = g.Key, totalMinor = g.Sum(p => p.AmountMinor), count = g.Count() }),
            });
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/reports/products", async (
            Guid venueId, int? days, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days ?? 7, 1, 90));

            var top = await (
                from item in db.Set<OrderItem>().AsNoTracking()
                join order in db.Set<Order>().AsNoTracking() on item.OrderId equals order.Id
                where order.VenueId == venueId && order.PlacedAt >= since
                      && CountedStatuses.Contains(order.Status)
                group item by item.ProductNameSnapshot into g
                orderby g.Sum(x => x.Quantity) descending
                select new
                {
                    productName = g.Key,
                    quantity = g.Sum(x => x.Quantity),
                    revenueMinor = g.Sum(x => x.LineTotalMinor),
                }
            ).Take(20).ToListAsync(ct);
            return Results.Ok(top);
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/reports/hourly", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var venueInfo = await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var (dayStart, dayEnd, _) = TodayRange(venueInfo.Timezone);
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(venueInfo.Timezone);

            var orders = await db.Set<Order>().AsNoTracking()
                .Where(o => o.VenueId == venueId && o.PlacedAt >= dayStart && o.PlacedAt < dayEnd
                            && CountedStatuses.Contains(o.Status))
                .Select(o => new { o.PlacedAt, o.TotalMinor })
                .ToListAsync(ct);
            var hourly = orders
                .GroupBy(o => TimeZoneInfo.ConvertTime(o.PlacedAt, timezone).Hour)
                .OrderBy(g => g.Key)
                .Select(g => new { hour = g.Key, orderCount = g.Count(), totalMinor = g.Sum(o => o.TotalMinor) });
            return Results.Ok(hourly);
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/reports/sales", async (
            Guid venueId, int? days, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var venueInfo = await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(venueInfo.Timezone);
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days ?? 7, 1, 90));

            var orders = await db.Set<Order>().AsNoTracking()
                .Where(o => o.VenueId == venueId && o.PlacedAt >= since
                            && CountedStatuses.Contains(o.Status))
                .Select(o => new { o.PlacedAt, o.TotalMinor })
                .ToListAsync(ct);
            var daily = orders
                .GroupBy(o => TimeZoneInfo.ConvertTime(o.PlacedAt, timezone).Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    orderCount = g.Count(),
                    totalMinor = g.Sum(o => o.TotalMinor),
                });
            return Results.Ok(daily);
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/reports/export", async (
            Guid venueId, int? days, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            var since = DateTimeOffset.UtcNow.AddDays(-Math.Clamp(days ?? 30, 1, 365));

            var orders = await db.Set<Order>().AsNoTracking()
                .Where(o => o.VenueId == venueId && o.PlacedAt >= since)
                .OrderBy(o => o.PlacedAt)
                .Select(o => new { o.OrderNumber, o.Status, o.TotalMinor, o.CurrencyCode, o.PlacedAt })
                .ToListAsync(ct);

            var csv = new StringBuilder("order_number,status,total_minor,currency,placed_at_utc\n");
            foreach (var order in orders)
            {
                csv.AppendLine(
                    $"{order.OrderNumber},{order.Status},{order.TotalMinor},{order.CurrencyCode},{order.PlacedAt:O}");
            }
            return Results.File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
                $"tinggo-orders-{DateTime.UtcNow:yyyyMMdd}.csv");
        }).RequireAuthorization();
    }

    private static (DateTimeOffset Start, DateTimeOffset End, DateTime LocalDate) TodayRange(string timezoneId)
    {
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
        var localStart = new DateTimeOffset(localNow.Date, localNow.Offset);
        return (localStart.ToUniversalTime(), localStart.AddDays(1).ToUniversalTime(), localNow.Date);
    }

    private static async Task<VenueInfo> EnsureStaffAsync(
        IVenueDirectory venues, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var venueInfo = await venues.GetVenueInfoAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        _ = await memberships.GetOrganizationRoleAsync(Guid.Parse(sub), venueInfo.OrganizationId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
        return venueInfo;
    }
}
