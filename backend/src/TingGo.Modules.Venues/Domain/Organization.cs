namespace TingGo.Modules.Venues.Domain;

public sealed class Organization
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Name { get; set; } = "";
    public string Status { get; set; } = "active";
    public string DefaultLocale { get; set; } = "vi-VN";
    public string DefaultCurrency { get; set; } = "VND";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
