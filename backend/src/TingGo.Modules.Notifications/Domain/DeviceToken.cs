namespace TingGo.Modules.Notifications.Domain;

/// <summary>Thiết bị nhận push (PRD 7.13) — mobile app đăng ký từ sprint mobile.</summary>
public sealed class DeviceToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string Platform { get; set; } = "";
    public string Token { get; set; } = "";
    public string? DeviceName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Gửi push notification. MVP: NoopPushSender (log); FCM cắm ở sprint mobile.</summary>
public interface IPushSender
{
    Task SendAsync(IReadOnlyCollection<string> deviceTokens, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default);
}
