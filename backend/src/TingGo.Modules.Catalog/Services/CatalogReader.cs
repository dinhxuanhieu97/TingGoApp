using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.SharedKernel.Contracts;

namespace TingGo.Modules.Catalog.Services;

public sealed class CatalogReader(TingGoDbContext db) : ICatalogReader
{
    public async Task<IReadOnlyDictionary<Guid, ProductSnapshot>> GetProductsAsync(
        Guid venueId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default)
    {
        var items = await db.Set<Product>().AsNoTracking()
            .Where(x => x.VenueId == venueId && productIds.Contains(x.Id))
            .Select(x => new ProductSnapshot(x.Id, x.Name, x.BasePriceMinor, x.CurrencyCode, x.IsAvailable, x.Status))
            .ToListAsync(ct);
        return items.ToDictionary(x => x.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, VariantSnapshot>> GetVariantsAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default)
    {
        var items = await db.Set<ProductVariant>().AsNoTracking()
            .Where(x => variantIds.Contains(x.Id))
            .Select(x => new VariantSnapshot(x.Id, x.ProductId, x.Name, x.PriceDeltaMinor, x.IsAvailable))
            .ToListAsync(ct);
        return items.ToDictionary(x => x.Id);
    }

    public async Task<IReadOnlyDictionary<Guid, OptionSnapshot>> GetOptionsAsync(
        Guid venueId, IReadOnlyCollection<Guid> optionIds, CancellationToken ct = default)
    {
        var items = await (
            from option in db.Set<ModifierOption>().AsNoTracking()
            join grp in db.Set<ModifierGroup>().AsNoTracking() on option.ModifierGroupId equals grp.Id
            where optionIds.Contains(option.Id) && grp.VenueId == venueId
            select new OptionSnapshot(option.Id, option.Name, option.PriceDeltaMinor, option.IsAvailable)
        ).ToListAsync(ct);
        return items.ToDictionary(x => x.Id);
    }
}
