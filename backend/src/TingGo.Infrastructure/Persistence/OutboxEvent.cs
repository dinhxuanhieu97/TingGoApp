namespace TingGo.Infrastructure.Persistence;

/// <summary>
/// Transactional outbox (PRD 6.3) — ghi cùng transaction với nghiệp vụ,
/// worker (Sprint 6) đọc và phát SignalR/push. KPI: 0% mất order sau khi DB xác nhận.
/// </summary>
public sealed class OutboxEvent
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public string AggregateType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "";
    public string Payload { get; set; } = "{}";
    public string Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
}

public static class OutboxStatus
{
    public const string Pending = "pending";
    public const string Processing = "processing";
    public const string Completed = "completed";
}

/// <summary>Idempotency key (PRD 6.3) — chống đơn trùng khi client retry.</summary>
public sealed class IdempotencyKey
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Scope { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public int ResponseStatus { get; set; }
    public string ResponseBody { get; set; } = "{}";
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(24);
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
