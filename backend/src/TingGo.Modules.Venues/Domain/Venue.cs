namespace TingGo.Modules.Venues.Domain;

public sealed class Venue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string CountryCode { get; set; } = "VN";
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string DefaultLocale { get; set; } = "vi-VN";
    public string CurrencyCode { get; set; } = "VND";
    public string Status { get; set; } = "active";
    public string? WifiName { get; set; }
    public string? WifiPasswordEncrypted { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
