using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Catalog.Endpoints;

/// <summary>CUS-02: menu công khai theo slug — không auth, có thể cache (NFR 5.1).</summary>
public static class PublicMenuEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/public/venues/{slug}/menu", async (
            string slug, HttpContext httpContext, TingGoDbContext db,
            TingGo.SharedKernel.Contracts.IVenueDirectory venueDirectory, CancellationToken ct) =>
        {
            // Cache CDN/browser 10s (NFR 5.1 — public menu có thể cache).
            httpContext.Response.Headers.CacheControl = "public, max-age=10";
            var venueRow = await venueDirectory.GetVenueBySlugAsync(slug, ct);
            if (venueRow is null || venueRow.Status != "active")
            {
                throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
            }

            var menu = await db.Set<Menu>().AsNoTracking()
                .Where(x => x.VenueId == venueRow.Id && x.Status == MenuStatus.Published)
                .OrderByDescending(x => x.PublishedAt)
                .FirstOrDefaultAsync(ct)
                ?? throw new ApiException(ErrorCodes.MenuNotPublished, "Quán chưa công bố menu.", 404);

            var categories = await db.Set<MenuCategory>().AsNoTracking()
                .Where(x => x.MenuId == menu.Id && x.IsVisible)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);

            var products = await db.Set<Product>().AsNoTracking()
                .Where(x => x.VenueId == venueRow.Id && x.Status == ProductStatus.Active)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
                .ToListAsync(ct);

            var productIds = products.Select(x => x.Id).ToList();
            var variants = await db.Set<ProductVariant>().AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId) && x.IsAvailable)
                .ToListAsync(ct);

            var links = await db.Set<ProductModifierGroup>().AsNoTracking()
                .Where(x => productIds.Contains(x.ProductId))
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);
            var groupIds = links.Select(x => x.ModifierGroupId).Distinct().ToList();
            var groups = await db.Set<ModifierGroup>().AsNoTracking()
                .Where(x => groupIds.Contains(x.Id)).ToListAsync(ct);
            var options = await db.Set<ModifierOption>().AsNoTracking()
                .Where(x => groupIds.Contains(x.ModifierGroupId) && x.IsAvailable)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);

            var groupDtos = groups.ToDictionary(g => g.Id, g => new
            {
                g.Id, g.Name, g.MinSelect, g.MaxSelect, g.IsRequired,
                options = options.Where(o => o.ModifierGroupId == g.Id)
                    .Select(o => new { o.Id, o.Name, o.PriceDeltaMinor }),
            });

            var result = new
            {
                venue = new { venueRow.Id, venueRow.Name, venueRow.Slug, venueRow.CurrencyCode, venueRow.DefaultLocale },
                menu = new { menu.Id, menu.Name, menu.PublishedAt },
                categories = categories.Select(c => new
                {
                    c.Id, c.Name,
                    products = products.Where(p => p.CategoryId == c.Id).Select(p => new
                    {
                        p.Id, p.Name, p.Description, p.BasePriceMinor, p.CurrencyCode,
                        p.ImageUrl, p.IsAvailable,
                        variants = variants.Where(v => v.ProductId == p.Id)
                            .Select(v => new { v.Id, v.Name, v.PriceDeltaMinor, v.IsDefault }),
                        modifierGroups = links.Where(l => l.ProductId == p.Id)
                            .Select(l => groupDtos.GetValueOrDefault(l.ModifierGroupId))
                            .Where(g => g is not null),
                    }),
                }),
            };

            return Results.Ok(result);
        });
    }
}
