namespace TingGo.SharedKernel.Contracts;

public sealed record ProductSnapshot(
    Guid Id, string Name, long BasePriceMinor, string CurrencyCode, bool IsAvailable, string Status);

public sealed record VariantSnapshot(
    Guid Id, Guid ProductId, string Name, long PriceDeltaMinor, bool IsAvailable);

public sealed record OptionSnapshot(
    Guid Id, string Name, long PriceDeltaMinor, bool IsAvailable);

/// <summary>Contract cross-module (impl tại Catalog) — Ordering đọc giá server-side để snapshot.</summary>
public interface ICatalogReader
{
    Task<IReadOnlyDictionary<Guid, ProductSnapshot>> GetProductsAsync(
        Guid venueId, IReadOnlyCollection<Guid> productIds, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, VariantSnapshot>> GetVariantsAsync(
        IReadOnlyCollection<Guid> variantIds, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, OptionSnapshot>> GetOptionsAsync(
        Guid venueId, IReadOnlyCollection<Guid> optionIds, CancellationToken ct = default);
}
