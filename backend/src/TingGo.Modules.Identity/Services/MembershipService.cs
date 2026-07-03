using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Identity.Domain;
using TingGo.SharedKernel.Contracts;

namespace TingGo.Modules.Identity.Services;

public sealed class MembershipService(TingGoDbContext db) : IMembershipService
{
    public async Task CreateOwnerMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        db.Add(new Membership
        {
            UserId = userId,
            OrganizationId = organizationId,
            VenueId = null,
            Role = MembershipRole.Owner,
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<string?> GetOrganizationRoleAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
        => db.Set<Membership>()
            .Where(x => x.UserId == userId
                        && x.OrganizationId == organizationId
                        && x.Status == MembershipStatus.Active)
            .Select(x => x.Role)
            .FirstOrDefaultAsync(ct);

    public async Task<(Guid MembershipId, string Role)?> GetMembershipAsync(
        Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var membership = await db.Set<Membership>().AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.OrganizationId == organizationId
                        && x.Status == MembershipStatus.Active)
            .Select(x => new { x.Id, x.Role })
            .FirstOrDefaultAsync(ct);
        return membership is null ? null : (membership.Id, membership.Role);
    }
}
