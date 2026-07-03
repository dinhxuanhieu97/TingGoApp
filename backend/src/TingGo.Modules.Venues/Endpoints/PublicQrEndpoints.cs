using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Venues.Endpoints;

/// <summary>CUS-01: khách quét QR — không cần đăng nhập.</summary>
public static class PublicQrEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/public/q/{qrToken}", async (
            string qrToken, TingGoDbContext db, CancellationToken ct) =>
        {
            var tokenHash = TableEndpoints.Sha256(qrToken);
            var qr = await db.Set<QrCode>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Mã QR không tồn tại.", 404);

            if (qr.Status != QrStatus.Active ||
                (qr.ExpiresAt is not null && qr.ExpiresAt <= DateTimeOffset.UtcNow))
            {
                throw new ApiException(ErrorCodes.QrRevoked,
                    "Mã QR đã bị thu hồi. Vui lòng gọi nhân viên để được hỗ trợ.", 410);
            }

            var table = await db.Set<DiningTable>().AsNoTracking()
                .FirstAsync(x => x.Id == qr.TableId, ct);
            if (table.Status != TableStatus.Active)
            {
                throw new ApiException(ErrorCodes.TableDisabled,
                    "Bàn này tạm thời không nhận order. Vui lòng gọi nhân viên.", 410);
            }

            var venue = await db.Set<Venue>().AsNoTracking()
                .FirstAsync(x => x.Id == table.VenueId, ct);
            if (venue.Status != "active")
            {
                throw new ApiException(ErrorCodes.NotFound, "Quán hiện không hoạt động.", 404);
            }

            var area = await db.Set<VenueArea>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == table.AreaId, ct);

            return Results.Ok(new
            {
                venue = new
                {
                    venue.Id, venue.Name, venue.Slug,
                    venue.CurrencyCode, venue.DefaultLocale, venue.Timezone, venue.WifiName,
                },
                table = new { table.Id, table.Code, table.Name },
                area = area is null ? null : new { area.Id, area.Name },
            });
        });
    }
}
