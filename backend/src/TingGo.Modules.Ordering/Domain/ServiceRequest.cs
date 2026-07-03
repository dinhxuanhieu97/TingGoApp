namespace TingGo.Modules.Ordering.Domain;

/// <summary>CUS-08: gọi nhân viên, xin thêm đồ, yêu cầu thanh toán.</summary>
public sealed class ServiceRequest
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid TableSessionId { get; set; }
    public string Type { get; set; } = ServiceRequestType.CallStaff;
    public string? Note { get; set; }
    public string Status { get; set; } = ServiceRequestStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
}

public static class ServiceRequestType
{
    public const string CallStaff = "call_staff";
    public const string Supplies = "supplies";
    public const string Payment = "payment";
    public static readonly string[] All = [CallStaff, Supplies, Payment];
}

public static class ServiceRequestStatus
{
    public const string Pending = "pending";
    public const string Acknowledged = "acknowledged";
    public const string Resolved = "resolved";
    public const string Cancelled = "cancelled";
}
