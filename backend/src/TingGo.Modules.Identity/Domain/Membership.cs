namespace TingGo.Modules.Identity.Domain;

public sealed class Membership
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }
    /// <summary>Null = quyền toàn tổ chức (owner).</summary>
    public Guid? VenueId { get; set; }
    public string Role { get; set; } = MembershipRole.Owner;
    public string Status { get; set; } = MembershipStatus.Active;
    public string? StaffCode { get; set; }
    public string? PinHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class MembershipRole
{
    public const string Owner = "owner";
    public const string Manager = "manager";
    public const string Cashier = "cashier";
    public const string Waiter = "waiter";
    public const string Kitchen = "kitchen";
}

public static class MembershipStatus
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}
