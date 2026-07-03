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
}
