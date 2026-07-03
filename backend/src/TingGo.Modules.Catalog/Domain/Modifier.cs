namespace TingGo.Modules.Catalog.Domain;

public sealed class ModifierGroup
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; } = 1;
    public bool IsRequired { get; set; }
}

public sealed class ModifierOption
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ModifierGroupId { get; set; }
    public string Name { get; set; } = "";
    public long PriceDeltaMinor { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>Gán nhóm tùy chọn cho món (many-to-many).</summary>
public sealed class ProductModifierGroup
{
    public Guid ProductId { get; set; }
    public Guid ModifierGroupId { get; set; }
    public int SortOrder { get; set; }
}
