using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Venues.Endpoints;

public sealed record OpeningHourDto(int DayOfWeek, string? OpenTime, string? CloseTime, bool IsClosed);
public sealed record PutOpeningHoursDto(List<OpeningHourDto> Days);

public static class OpeningHourEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/opening-hours", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        }).RequireAuthorization();

        // Thay toàn bộ giờ mở cửa (7 ngày) trong một lần lưu
        endpoints.MapPut("/venues/{venueId:guid}/opening-hours", async (
            Guid venueId, PutOpeningHoursDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            var role = await EnsureStaffAsync(venues, memberships, principal, venueId, ct);
            if (role is not ("owner" or "manager"))
            {
                throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được sửa giờ mở cửa.", 403);
            }

            var parsed = new List<OpeningHour>();
            foreach (var day in dto.Days)
            {
                if (day.DayOfWeek is < 1 or > 7)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "day_of_week phải từ 1 (Thứ Hai) đến 7 (Chủ Nhật).", 400);
                }
                if (parsed.Any(x => x.DayOfWeekIso == day.DayOfWeek))
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, $"Ngày {day.DayOfWeek} bị lặp.", 400);
                }
                TimeOnly? open = null, close = null;
                if (!day.IsClosed)
                {
                    if (!TimeOnly.TryParse(day.OpenTime ?? "", out var openParsed)
                        || !TimeOnly.TryParse(day.CloseTime ?? "", out var closeParsed))
                    {
                        throw new ApiException(ErrorCodes.ValidationFailed,
                            $"Ngày {day.DayOfWeek}: giờ phải dạng HH:mm (hoặc đánh dấu nghỉ).", 400);
                    }
                    open = openParsed;
                    close = closeParsed;
                }
                parsed.Add(new OpeningHour
                {
                    VenueId = venueId, DayOfWeekIso = day.DayOfWeek,
                    OpenTime = open, CloseTime = close, IsClosed = day.IsClosed,
                });
            }

            await db.Set<OpeningHour>().Where(x => x.VenueId == venueId).ExecuteDeleteAsync(ct);
            db.AddRange(parsed);
            await db.SaveChangesAsync(ct);
            return Results.Ok(await LoadAsync(db, venueId, ct));
        }).RequireAuthorization();
    }

    private static async Task<IReadOnlyList<object>> LoadAsync(TingGoDbContext db, Guid venueId, CancellationToken ct)
        => await db.Set<OpeningHour>().AsNoTracking()
            .Where(x => x.VenueId == venueId)
            .OrderBy(x => x.DayOfWeekIso)
            .Select(x => (object)new
            {
                dayOfWeek = x.DayOfWeekIso,
                openTime = x.OpenTime != null ? x.OpenTime.Value.ToString("HH:mm") : null,
                closeTime = x.CloseTime != null ? x.CloseTime.Value.ToString("HH:mm") : null,
                x.IsClosed,
            })
            .ToListAsync(ct);

    /// <summary>Trạng thái mở cửa hiện tại theo timezone quán — hỗ trợ ca qua nửa đêm.</summary>
    public static (bool? IsOpenNow, string? TodayLabel) EvaluateOpenNow(
        IReadOnlyList<OpeningHour> hours, string timezoneId)
    {
        if (hours.Count == 0) return (null, null);
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var localNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timezone);
        var isoToday = localNow.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)localNow.DayOfWeek;
        var now = TimeOnly.FromDateTime(localNow.DateTime);

        var today = hours.FirstOrDefault(x => x.DayOfWeekIso == isoToday);
        var label = today is null || today.IsClosed || today.OpenTime is null
            ? "Hôm nay nghỉ"
            : $"{today.OpenTime:HH\\:mm}–{today.CloseTime:HH\\:mm}";

        bool open = false;
        if (today is { IsClosed: false, OpenTime: not null, CloseTime: not null })
        {
            open = today.CloseTime > today.OpenTime
                ? now >= today.OpenTime && now < today.CloseTime
                : now >= today.OpenTime || now < today.CloseTime; // ca qua nửa đêm
        }
        // Ca hôm qua kéo qua nửa đêm (VD mở 20:00–02:00, hiện 01:00)
        if (!open)
        {
            var isoYesterday = isoToday == 1 ? 7 : isoToday - 1;
            var yesterday = hours.FirstOrDefault(x => x.DayOfWeekIso == isoYesterday);
            if (yesterday is { IsClosed: false, OpenTime: not null, CloseTime: not null }
                && yesterday.CloseTime < yesterday.OpenTime && now < yesterday.CloseTime)
            {
                open = true;
                label = $"{yesterday.OpenTime:HH\\:mm}–{yesterday.CloseTime:HH\\:mm}";
            }
        }
        return (open, label);
    }

    private static async Task<string> EnsureStaffAsync(
        IVenueDirectory venues, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var organizationId = await venues.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return await memberships.GetOrganizationRoleAsync(Guid.Parse(sub), organizationId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
    }
}
