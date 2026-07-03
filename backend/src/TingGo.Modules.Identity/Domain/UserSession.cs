namespace TingGo.Modules.Identity.Domain;

/// <summary>Refresh token session — lưu hash, không lưu raw token.</summary>
public sealed class UserSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public string RefreshTokenHash { get; set; } = "";
    public string? DeviceName { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>Session thay thế khi rotate — phát hiện token reuse.</summary>
    public Guid? ReplacedBySessionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}
