using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.Modules.Catalog.Services;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Catalog.Endpoints;

public sealed record CreateMenuDto(string Name);
public sealed record UpdateMenuDto(string? Name);
public sealed record CreateCategoryDto(string Name, int? SortOrder);
public sealed record UpdateCategoryDto(string? Name, bool? IsVisible);
public sealed record SortCategoriesDto(List<Guid> CategoryIds);

public static class MenuEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/menus", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.EnsureStaffAsync(principal, venueId, ct);
            var items = await db.Set<Menu>().AsNoTracking()
                .Where(x => x.VenueId == venueId)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/menus", async (
            Guid venueId, CreateMenuDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.EnsureManagerAsync(principal, venueId, ct);
            ValidateName(dto.Name, "Tên menu");
            var menu = new Menu { VenueId = venueId, Name = dto.Name.Trim() };
            db.Add(menu);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/menus/{menu.Id}", menu);
        }).RequireAuthorization();

        endpoints.MapGet("/menus/{menuId:guid}", async (
            Guid menuId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var menu = await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            var categories = await db.Set<MenuCategory>().AsNoTracking()
                .Where(x => x.MenuId == menuId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);
            return Results.Ok(new { menu.Id, menu.VenueId, menu.Name, menu.Status, menu.PublishedAt, categories });
        }).RequireAuthorization();

        endpoints.MapPatch("/menus/{menuId:guid}", async (
            Guid menuId, UpdateMenuDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            CatalogGuard guard, CancellationToken ct) =>
        {
            var menu = await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            if (dto.Name is not null)
            {
                ValidateName(dto.Name, "Tên menu");
                menu.Name = dto.Name.Trim();
            }
            menu.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(menu);
        }).RequireAuthorization();

        endpoints.MapPost("/menus/{menuId:guid}/publish", async (
            Guid menuId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var menu = await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            var hasVisibleCategory = await db.Set<MenuCategory>()
                .AnyAsync(x => x.MenuId == menuId && x.IsVisible, ct);
            if (!hasVisibleCategory)
            {
                throw new ApiException(ErrorCodes.MenuNotPublished,
                    "Menu cần ít nhất một danh mục hiển thị trước khi công bố.", 400);
            }
            menu.Status = MenuStatus.Published;
            menu.PublishedAt = DateTimeOffset.UtcNow;
            menu.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(menu);
        }).RequireAuthorization();

        endpoints.MapPost("/menus/{menuId:guid}/unpublish", async (
            Guid menuId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var menu = await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            menu.Status = MenuStatus.Draft;
            menu.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(menu);
        }).RequireAuthorization();

        endpoints.MapGet("/menus/{menuId:guid}/categories", async (
            Guid menuId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            var items = await db.Set<MenuCategory>().AsNoTracking()
                .Where(x => x.MenuId == menuId).OrderBy(x => x.SortOrder).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/menus/{menuId:guid}/categories", async (
            Guid menuId, CreateCategoryDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            ValidateName(dto.Name, "Tên danh mục");
            var sortOrder = dto.SortOrder
                ?? (await db.Set<MenuCategory>().Where(x => x.MenuId == menuId).MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 1;
            var category = new MenuCategory { MenuId = menuId, Name = dto.Name.Trim(), SortOrder = sortOrder };
            db.Add(category);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/categories/{category.Id}", category);
        }).RequireAuthorization();

        endpoints.MapPatch("/categories/{categoryId:guid}", async (
            Guid categoryId, UpdateCategoryDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            CatalogGuard guard, CancellationToken ct) =>
        {
            var category = await guard.LoadCategoryAsManagerAsync(principal, categoryId, ct);
            if (dto.Name is not null)
            {
                ValidateName(dto.Name, "Tên danh mục");
                category.Name = dto.Name.Trim();
            }
            category.IsVisible = dto.IsVisible ?? category.IsVisible;
            category.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(category);
        }).RequireAuthorization();

        endpoints.MapDelete("/categories/{categoryId:guid}", async (
            Guid categoryId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var category = await guard.LoadCategoryAsManagerAsync(principal, categoryId, ct);
            var hasProducts = await db.Set<Product>()
                .AnyAsync(x => x.CategoryId == categoryId && x.Status == ProductStatus.Active, ct);
            if (hasProducts)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    "Danh mục còn món đang bán. Hãy chuyển/lưu trữ món trước, hoặc ẩn danh mục (isVisible=false).", 409);
            }
            db.Remove(category);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        endpoints.MapPut("/menus/{menuId:guid}/categories/sort", async (
            Guid menuId, SortCategoriesDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.LoadMenuAsManagerAsync(principal, menuId, ct);
            var categories = await db.Set<MenuCategory>().Where(x => x.MenuId == menuId).ToListAsync(ct);
            var order = dto.CategoryIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            foreach (var category in categories)
            {
                if (order.TryGetValue(category.Id, out var sortOrder))
                {
                    category.SortOrder = sortOrder;
                    category.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    internal static void ValidateName(string name, string label)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, $"{label} không hợp lệ (1–200 ký tự).", 400);
        }
    }
}
