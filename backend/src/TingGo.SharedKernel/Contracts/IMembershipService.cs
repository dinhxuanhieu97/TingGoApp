namespace TingGo.SharedKernel.Contracts;

/// <summary>
/// Contract cross-module (impl tại Identity). Venues dùng khi onboarding:
/// tạo organization → gán owner membership.
/// </summary>
public interface IMembershipService
{
    Task CreateOwnerMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default);

    /// <summary>Trả về role của user trong organization (membership active), null nếu không có.</summary>
    Task<string?> GetOrganizationRoleAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
}
