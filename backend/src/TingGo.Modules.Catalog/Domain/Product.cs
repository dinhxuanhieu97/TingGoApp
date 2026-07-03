namespace TingGo.Modules.Catalog.Domain;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid CategoryId { get; set; }
    public string? Sku { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Giá đơn vị nhỏ nhất (BIGINT minor) — KHÔNG dùng float/decimal.</summary>
    public long BasePriceMinor { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = ProductStatus.Active;
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class ProductStatus
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public sealed class ProductVariant
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "";
    public long PriceDeltaMinor { get; set; }
    public bool IsDefault { get; set; }
    public bool IsAvailable { get; set; } = true;
}
