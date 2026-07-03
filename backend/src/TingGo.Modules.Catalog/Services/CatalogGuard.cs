using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Catalog.Services;

/// <summary>Tenant isolation + phân quyền cho Catalog: mọi thao tác ghi cần owner/manager của venue.</summary>
public sealed class CatalogGuard(TingGoDbContext db, IVenueDirectory venues, IMembershipService memberships)
{
    public async Task EnsureManagerAsync(ClaimsPrincipal principal, Guid venueId, CancellationToken ct)
    {
        var organizationId = await venues.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);

        var role = await memberships.GetOrganizationRoleAsync(GetUserId(principal), organizationId, ct);
        if (role is not ("owner" or "manager"))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được quản lý menu.", 403);
        }
    }

    /// <summary>Nhân viên (mọi role active) được phép bật/tắt món (MOB-04).</summary>
    public async Task EnsureStaffAsync(ClaimsPrincipal principal, Guid venueId, CancellationToken ct)
    {
        var organizationId = await venues.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);

        _ = await memberships.GetOrganizationRoleAsync(GetUserId(principal), organizationId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
    }

    public async Task<Menu> LoadMenuAsManagerAsync(ClaimsPrincipal principal, Guid menuId, CancellationToken ct)
    {
        var menu = await db.Set<Menu>().FirstOrDefaultAsync(x => x.Id == menuId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy menu.", 404);
        await EnsureManagerAsync(principal, menu.VenueId, ct);
        return menu;
    }

    public async Task<MenuCategory> LoadCategoryAsManagerAsync(ClaimsPrincipal principal, Guid categoryId, CancellationToken ct)
    {
        var category = await db.Set<MenuCategory>().FirstOrDefaultAsync(x => x.Id == categoryId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy danh mục.", 404);
        var menu = await db.Set<Menu>().AsNoTracking().FirstAsync(x => x.Id == category.MenuId, ct);
        await EnsureManagerAsync(principal, menu.VenueId, ct);
        return category;
    }

    public async Task<Product> LoadProductAsManagerAsync(ClaimsPrincipal principal, Guid productId, CancellationToken ct)
    {
        var product = await db.Set<Product>().FirstOrDefaultAsync(x => x.Id == productId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy món.", 404);
        await EnsureManagerAsync(principal, product.VenueId, ct);
        return product;
    }

    public async Task<ModifierGroup> LoadModifierGroupAsManagerAsync(ClaimsPrincipal principal, Guid groupId, CancellationToken ct)
    {
        var group = await db.Set<ModifierGroup>().FirstOrDefaultAsync(x => x.Id == groupId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy nhóm tùy chọn.", 404);
        await EnsureManagerAsync(principal, group.VenueId, ct);
        return group;
    }

    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return Guid.Parse(sub);
    }
}
