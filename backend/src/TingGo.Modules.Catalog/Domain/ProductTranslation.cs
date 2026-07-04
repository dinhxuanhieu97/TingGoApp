namespace TingGo.Modules.Catalog.Domain;

/// <summary>Bản dịch tên/mô tả món (PRD 5.4 / ERD product_translations).</summary>
public sealed class ProductTranslation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ProductId { get; set; }
    public string Locale { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}
