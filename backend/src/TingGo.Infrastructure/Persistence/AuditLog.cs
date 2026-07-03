namespace TingGo.Infrastructure.Persistence;

/// <summary>Audit log (PRD 5.3) — ghi hành động nhạy cảm: hủy/từ chối order, đóng/mở bàn, tạo nhân viên.</summary>
public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public Guid EntityId { get; set; }
    public string? Detail { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
