namespace TingGo.Api.Import;

// Normalized data lưu trong import_rows.normalized_data (jsonb) — committer đọc lại.
public sealed record VenueRowData(
    string? WifiName, string? DefaultLocale, string? CurrencyCode, string? Timezone);

public sealed record AreaRowData(string Code, string Name, int SortOrder, bool IsActive);

public sealed record TableRowData(string Code, string Name, string AreaCode, int SortOrder, bool IsActive);

public sealed record CategoryRowData(string Code, string Name, string? Description, int SortOrder, bool IsVisible);

public sealed record ProductRowData(
    string Code, string CategoryCode, string Name, string? Description,
    long BasePriceMinor, string? ImageFile, bool IsAvailable, int SortOrder);

public sealed record VariantRowData(
    string Code, string ProductCode, string Name, long PriceDeltaMinor,
    bool IsDefault, bool IsAvailable, int SortOrder);

public sealed record GroupRowData(
    string Code, string Name, int MinSelect, int MaxSelect, bool IsRequired, int SortOrder);

public sealed record OptionRowData(
    string Code, string GroupCode, string Name, long PriceDeltaMinor, bool IsAvailable, int SortOrder);

public sealed record ProductModifierRowData(string ProductCode, string GroupCode, int SortOrder);

public static class ImportSections
{
    public const string Venue = "VENUE";
    public const string Areas = "AREAS";
    public const string Tables = "TABLES";
    public const string Categories = "CATEGORIES";
    public const string Products = "PRODUCTS";
    public const string Variants = "VARIANTS";
    public const string ModifierGroups = "MODIFIER_GROUPS";
    public const string ModifierOptions = "MODIFIER_OPTIONS";
    public const string ProductModifiers = "PRODUCT_MODIFIERS";
}
