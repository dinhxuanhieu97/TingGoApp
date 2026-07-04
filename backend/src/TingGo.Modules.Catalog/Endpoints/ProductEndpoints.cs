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

public sealed record CreateProductDto(
    Guid CategoryId, string Name, string? Description, long BasePriceMinor,
    string? Sku, string? ImageUrl, int? SortOrder);
public sealed record UpdateProductDto(
    Guid? CategoryId, string? Name, string? Description, long? BasePriceMinor,
    string? Sku, string? ImageUrl, int? SortOrder, long RowVersion);
public sealed record AvailabilityDto(bool IsAvailable);
public sealed record CreateVariantDto(string Name, long PriceDeltaMinor, bool IsDefault);
public sealed record UpdateVariantDto(string? Name, long? PriceDeltaMinor, bool? IsDefault, bool? IsAvailable);

public static class ProductEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/venues/{venueId:guid}/products", async (
            Guid venueId, string? search, Guid? categoryId, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.EnsureStaffAsync(principal, venueId, ct);
            var query = db.Set<Product>().AsNoTracking()
                .Where(x => x.VenueId == venueId && x.Status == ProductStatus.Active);
            if (categoryId is not null) query = query.Where(x => x.CategoryId == categoryId);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = $"%{search.Trim()}%";
                query = query.Where(x => EF.Functions.ILike(x.Name, term));
            }
            var items = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/products", async (
            Guid venueId, CreateProductDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard,
            TingGo.SharedKernel.Contracts.IVenueDirectory venueDirectory, CancellationToken ct) =>
        {
            await guard.EnsureManagerAsync(principal, venueId, ct);
            MenuEndpoints.ValidateName(dto.Name, "Tên món");
            ValidatePrice(dto.BasePriceMinor);

            var category = await db.Set<MenuCategory>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.CategoryId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy danh mục.", 404);
            var menu = await db.Set<Menu>().AsNoTracking().FirstAsync(x => x.Id == category.MenuId, ct);
            if (menu.VenueId != venueId)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Danh mục không thuộc quán này.", 400);
            }

            // Không hard-code VND — lấy currency từ venue (PRD 5.4).
            var venueInfo = await venueDirectory.GetVenueInfoAsync(venueId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
            var currency = venueInfo.CurrencyCode;

            var product = new Product
            {
                VenueId = venueId,
                CategoryId = dto.CategoryId,
                Name = dto.Name.Trim(),
                Description = dto.Description,
                BasePriceMinor = dto.BasePriceMinor,
                CurrencyCode = currency,
                Sku = dto.Sku,
                ImageUrl = dto.ImageUrl,
                SortOrder = dto.SortOrder ?? 0,
            };
            db.Add(product);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/products/{product.Id}", product);
        }).RequireAuthorization();

        endpoints.MapGet("/products/{productId:guid}", async (
            Guid productId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await guard.LoadProductAsManagerAsync(principal, productId, ct);
            var variants = await db.Set<ProductVariant>().AsNoTracking()
                .Where(x => x.ProductId == productId).ToListAsync(ct);
            var groupIds = await db.Set<ProductModifierGroup>().AsNoTracking()
                .Where(x => x.ProductId == productId).OrderBy(x => x.SortOrder)
                .Select(x => x.ModifierGroupId).ToListAsync(ct);
            return Results.Ok(new { product, variants, modifierGroupIds = groupIds });
        }).RequireAuthorization();

        endpoints.MapPatch("/products/{productId:guid}", async (
            Guid productId, UpdateProductDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await guard.LoadProductAsManagerAsync(principal, productId, ct);
            if (product.RowVersion != dto.RowVersion)
            {
                throw new ApiException(ErrorCodes.Conflict, "Món đã được sửa bởi người khác. Hãy tải lại.", 409,
                    new Dictionary<string, object?> { ["currentRowVersion"] = product.RowVersion });
            }
            if (dto.Name is not null)
            {
                MenuEndpoints.ValidateName(dto.Name, "Tên món");
                product.Name = dto.Name.Trim();
            }
            if (dto.BasePriceMinor is not null)
            {
                ValidatePrice(dto.BasePriceMinor.Value);
                product.BasePriceMinor = dto.BasePriceMinor.Value;
            }
            if (dto.CategoryId is not null)
            {
                var category = await db.Set<MenuCategory>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == dto.CategoryId, ct)
                    ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy danh mục.", 404);
                var menu = await db.Set<Menu>().AsNoTracking().FirstAsync(x => x.Id == category.MenuId, ct);
                if (menu.VenueId != product.VenueId)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Danh mục không thuộc quán này.", 400);
                }
                product.CategoryId = dto.CategoryId.Value;
            }
            // Gửi chuỗi rỗng để xóa mô tả; null/không gửi = giữ nguyên
            if (dto.Description is not null)
            {
                product.Description = dto.Description.Length == 0 ? null : dto.Description;
            }
            product.Sku = dto.Sku ?? product.Sku;
            product.ImageUrl = dto.ImageUrl ?? product.ImageUrl;
            product.SortOrder = dto.SortOrder ?? product.SortOrder;
            product.RowVersion++;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        }).RequireAuthorization();

        endpoints.MapPost("/products/{productId:guid}/archive", async (
            Guid productId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await guard.LoadProductAsManagerAsync(principal, productId, ct);
            product.Status = ProductStatus.Archived;
            product.IsAvailable = false;
            product.RowVersion++;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        }).RequireAuthorization();

        // MOB-04: nhân viên (mọi role) bật/tắt món nhanh — không cần rowVersion.
        endpoints.MapPatch("/products/{productId:guid}/availability", async (
            Guid productId, AvailabilityDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await db.Set<Product>().FirstOrDefaultAsync(x => x.Id == productId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy món.", 404);
            await guard.EnsureStaffAsync(principal, product.VenueId, ct);
            product.IsAvailable = dto.IsAvailable;
            product.RowVersion++;
            product.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { product.Id, product.IsAvailable, product.RowVersion });
        }).RequireAuthorization();

        endpoints.MapPost("/products/{productId:guid}/variants", async (
            Guid productId, CreateVariantDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await guard.LoadProductAsManagerAsync(principal, productId, ct);
            MenuEndpoints.ValidateName(dto.Name, "Tên size");
            if (dto.IsDefault)
            {
                await db.Set<ProductVariant>()
                    .Where(x => x.ProductId == productId && x.IsDefault)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
            }
            var variant = new ProductVariant
            {
                ProductId = product.Id,
                Name = dto.Name.Trim(),
                PriceDeltaMinor = dto.PriceDeltaMinor,
                IsDefault = dto.IsDefault,
            };
            db.Add(variant);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/product-variants/{variant.Id}", variant);
        }).RequireAuthorization();

        endpoints.MapPatch("/product-variants/{variantId:guid}", async (
            Guid variantId, UpdateVariantDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var variant = await db.Set<ProductVariant>().FirstOrDefaultAsync(x => x.Id == variantId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy size.", 404);
            await guard.LoadProductAsManagerAsync(principal, variant.ProductId, ct);
            if (dto.Name is not null)
            {
                MenuEndpoints.ValidateName(dto.Name, "Tên size");
                variant.Name = dto.Name.Trim();
            }
            variant.PriceDeltaMinor = dto.PriceDeltaMinor ?? variant.PriceDeltaMinor;
            variant.IsAvailable = dto.IsAvailable ?? variant.IsAvailable;
            if (dto.IsDefault == true)
            {
                await db.Set<ProductVariant>()
                    .Where(x => x.ProductId == variant.ProductId && x.IsDefault && x.Id != variantId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), ct);
                variant.IsDefault = true;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(variant);
        }).RequireAuthorization();

        endpoints.MapDelete("/product-variants/{variantId:guid}", async (
            Guid variantId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var variant = await db.Set<ProductVariant>().FirstOrDefaultAsync(x => x.Id == variantId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy size.", 404);
            await guard.LoadProductAsManagerAsync(principal, variant.ProductId, ct);
            db.Remove(variant);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static void ValidatePrice(long priceMinor)
    {
        if (priceMinor < 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Giá không được âm.", 400);
        }
    }
}
