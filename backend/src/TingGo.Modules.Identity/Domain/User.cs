namespace TingGo.Modules.Identity.Domain;

public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string? Email { get; set; }
    public string? PhoneE164 { get; set; }
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = UserStatus.Active;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class UserStatus
{
    public const string Active = "active";
    public const string Blocked = "blocked";
}
