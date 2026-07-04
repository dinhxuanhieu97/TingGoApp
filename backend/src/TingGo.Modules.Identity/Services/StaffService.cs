using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Identity.Auth;
using TingGo.Modules.Identity.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Identity.Services;

public sealed record StaffCreated(Guid MembershipId, Guid UserId, string DisplayName, string Role, string StaffCode);

public sealed class StaffService(TingGoDbContext db, IVenueDirectory venueDirectory, IMembershipService memberships)
{
    private static readonly string[] AllowedRoles =
        [MembershipRole.Manager, MembershipRole.Cashier, MembershipRole.Waiter, MembershipRole.Kitchen];

    public async Task<StaffCreated> CreateStaffAsync(
        Guid callerUserId, Guid venueId, string displayName, string role, string? staffCode, string pin,
        CancellationToken ct)
    {
        var organizationId = await EnsureManagerAsync(callerUserId, venueId, ct);

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 200)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Tên nhân viên không hợp lệ.", 400);
        }
        if (!AllowedRoles.Contains(role))
        {
            throw new ApiException(ErrorCodes.ValidationFailed,
                $"Role phải là: {string.Join(", ", AllowedRoles)}.", 400);
        }
        if (pin.Length is < 4 or > 6 || !pin.All(char.IsAsciiDigit))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "PIN phải gồm 4–6 chữ số.", 400);
        }

        staffCode = staffCode?.Trim().ToUpperInvariant();
        if (staffCode is not null)
        {
            var exists = await db.Set<Membership>()
                .AnyAsync(x => x.VenueId == venueId && x.StaffCode == staffCode, ct);
            if (exists)
            {
                throw new ApiException(ErrorCodes.Conflict, "Mã nhân viên đã tồn tại trong quán.", 409);
            }
        }
        else
        {
            staffCode = await GenerateStaffCodeAsync(venueId, ct);
        }

        var user = new User { DisplayName = displayName.Trim() };
        var membership = new Membership
        {
            UserId = user.Id,
            OrganizationId = organizationId,
            VenueId = venueId,
            Role = role,
            StaffCode = staffCode,
            PinHash = PinHashing.Hash(pin),
        };
        db.Add(user);
        db.Add(membership);
        await db.SaveChangesAsync(ct);

        return new StaffCreated(membership.Id, user.Id, user.DisplayName, role, staffCode);
    }

    public async Task<IReadOnlyList<object>> ListStaffAsync(Guid callerUserId, Guid venueId, CancellationToken ct)
    {
        await EnsureManagerAsync(callerUserId, venueId, ct);

        return await db.Set<Membership>().AsNoTracking()
            .Where(m => m.VenueId == venueId)
            .Join(db.Set<User>().AsNoTracking(), m => m.UserId, u => u.Id,
                (m, u) => new { m.Id, m.UserId, u.DisplayName, m.Role, m.StaffCode, m.Status })
            .ToListAsync(ct);
    }

    public async Task ResetPinAsync(Guid callerUserId, Guid venueId, Guid membershipId, string pin, CancellationToken ct)
    {
        await EnsureManagerAsync(callerUserId, venueId, ct);
        if (pin.Length is < 4 or > 6 || !pin.All(char.IsAsciiDigit))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "PIN phải gồm 4–6 chữ số.", 400);
        }
        var membership = await LoadStaffAsync(venueId, membershipId, ct);
        membership.PinHash = PinHashing.Hash(pin);
        membership.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetStatusAsync(Guid callerUserId, Guid venueId, Guid membershipId, bool active, CancellationToken ct)
    {
        await EnsureManagerAsync(callerUserId, venueId, ct);
        var membership = await LoadStaffAsync(venueId, membershipId, ct);
        if (membership.Role == MembershipRole.Owner)
        {
            throw new ApiException(ErrorCodes.Forbidden, "Không thể thu hồi quyền owner.", 403);
        }
        membership.Status = active ? MembershipStatus.Active : MembershipStatus.Revoked;
        membership.UpdatedAt = DateTimeOffset.UtcNow;
        // Thu hồi → đăng xuất mọi thiết bị của nhân viên (MOB-01)
        if (!active)
        {
            await db.Set<UserSession>()
                .Where(x => x.UserId == membership.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<Membership> LoadStaffAsync(Guid venueId, Guid membershipId, CancellationToken ct)
        => await db.Set<Membership>()
               .FirstOrDefaultAsync(x => x.Id == membershipId && x.VenueId == venueId, ct)
           ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy nhân viên.", 404);

    /// <summary>Caller phải là owner/manager của organization chứa venue. Trả về organizationId.</summary>
    private async Task<Guid> EnsureManagerAsync(Guid callerUserId, Guid venueId, CancellationToken ct)
    {
        var organizationId = await venueDirectory.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);

        var role = await memberships.GetOrganizationRoleAsync(callerUserId, organizationId, ct);
        if (role is not (MembershipRole.Owner or MembershipRole.Manager))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được quản lý nhân viên.", 403);
        }
        return organizationId;
    }

    private async Task<string> GenerateStaffCodeAsync(Guid venueId, CancellationToken ct)
    {
        var existing = await db.Set<Membership>()
            .Where(x => x.VenueId == venueId && x.StaffCode != null)
            .Select(x => x.StaffCode!)
            .ToListAsync(ct);
        var taken = existing.ToHashSet();
        for (var i = 1; i < 1000; i++)
        {
            var code = $"NV{i:D2}";
            if (!taken.Contains(code)) return code;
        }
        throw new ApiException(ErrorCodes.Conflict, "Không sinh được mã nhân viên.", 409);
    }
}
