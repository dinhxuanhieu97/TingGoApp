namespace TingGo.Modules.Ordering.Domain;

public sealed class TableSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid TableId { get; set; }
    public string PublicTokenHash { get; set; } = "";
    public string Status { get; set; } = TableSessionStatus.Open;
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }
    public long RowVersion { get; set; }
}

public static class TableSessionStatus
{
    public const string Open = "open";
    public const string PaymentPending = "payment_pending";
    public const string Closed = "closed";
}
