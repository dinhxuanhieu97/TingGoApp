using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Contracts;

namespace TingGo.Modules.Venues.Services;

public sealed class VenueDirectory(TingGoDbContext db) : IVenueDirectory
{
    public async Task<Guid?> GetOrganizationIdAsync(Guid venueId, CancellationToken ct = default)
    {
        var orgId = await db.Set<Venue>().AsNoTracking()
            .Where(x => x.Id == venueId)
            .Select(x => (Guid?)x.OrganizationId)
            .FirstOrDefaultAsync(ct);
        return orgId;
    }

    public Task<VenueInfo?> GetVenueInfoAsync(Guid venueId, CancellationToken ct = default)
        => db.Set<Venue>().AsNoTracking()
            .Where(x => x.Id == venueId)
            .Select(x => new VenueInfo(x.Id, x.OrganizationId, x.CurrencyCode, x.DefaultLocale,
                x.Timezone, x.Status, x.Name, x.Slug))
            .FirstOrDefaultAsync(ct)!;

    public Task<VenueInfo?> GetVenueBySlugAsync(string slug, CancellationToken ct = default)
        => db.Set<Venue>().AsNoTracking()
            .Where(x => x.Slug == slug)
            .Select(x => new VenueInfo(x.Id, x.OrganizationId, x.CurrencyCode, x.DefaultLocale,
                x.Timezone, x.Status, x.Name, x.Slug))
            .FirstOrDefaultAsync(ct)!;

    public async Task<Guid?> GetVenueIdByJoinCodeAsync(string joinCode, CancellationToken ct = default)
    {
        var id = await db.Set<Venue>().AsNoTracking()
            .Where(x => x.JoinCode == joinCode.ToUpperInvariant())
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        return id;
    }

    public async Task<TableInfo?> GetActiveTableByQrTokenAsync(string rawQrToken, CancellationToken ct = default)
    {
        var tokenHash = Endpoints.TableEndpoints.Sha256(rawQrToken);
        var result = await (
            from qr in db.Set<QrCode>().AsNoTracking()
            join table in db.Set<DiningTable>().AsNoTracking() on qr.TableId equals table.Id
            join venue in db.Set<Venue>().AsNoTracking() on table.VenueId equals venue.Id
            where qr.TokenHash == tokenHash
                  && qr.Status == QrStatus.Active
                  && table.Status == TableStatus.Active
                  && venue.Status == "active"
            select new TableInfo(table.Id, table.VenueId, table.Code, table.Name)
        ).FirstOrDefaultAsync(ct);
        return result;
    }
}
