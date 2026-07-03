namespace TingGo.Modules.Venues.Domain;

public sealed class VenueArea
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public sealed class DiningTable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid AreaId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Status { get; set; } = TableStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class TableStatus
{
    public const string Active = "active";
    public const string Disabled = "disabled";
}

/// <summary>QR gắn bàn — chỉ lưu hash, raw token chỉ trả về lúc tạo/tạo lại.</summary>
public sealed class QrCode
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid TableId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Status { get; set; } = QrStatus.Active;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class QrStatus
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}
