namespace TingGo.Modules.Catalog.Domain;

public sealed class Menu
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = MenuStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class MenuStatus
{
    public const string Draft = "draft";
    public const string Published = "published";
}

public sealed class MenuCategory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MenuId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
