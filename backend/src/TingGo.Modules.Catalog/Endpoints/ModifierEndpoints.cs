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

public sealed record CreateModifierGroupDto(string Name, int MinSelect, int MaxSelect, bool IsRequired);
public sealed record UpdateModifierGroupDto(string? Name, int? MinSelect, int? MaxSelect, bool? IsRequired);
public sealed record CreateModifierOptionDto(string Name, long PriceDeltaMinor, int? SortOrder);
public sealed record UpdateModifierOptionDto(string? Name, long? PriceDeltaMinor, bool? IsAvailable, int? SortOrder);
public sealed record AssignModifierGroupsDto(List<Guid> ModifierGroupIds);

public static class ModifierEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/venues/{venueId:guid}/modifier-groups", async (
            Guid venueId, CreateModifierGroupDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.EnsureManagerAsync(principal, venueId, ct);
            MenuEndpoints.ValidateName(dto.Name, "Tên nhóm tùy chọn");
            ValidateSelectRange(dto.MinSelect, dto.MaxSelect);
            var group = new ModifierGroup
            {
                VenueId = venueId,
                Name = dto.Name.Trim(),
                MinSelect = dto.MinSelect,
                MaxSelect = dto.MaxSelect,
                IsRequired = dto.IsRequired,
            };
            db.Add(group);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/modifier-groups/{group.Id}", group);
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/modifier-groups", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            await guard.EnsureStaffAsync(principal, venueId, ct);
            var groups = await db.Set<ModifierGroup>().AsNoTracking()
                .Where(x => x.VenueId == venueId).ToListAsync(ct);
            var groupIds = groups.Select(x => x.Id).ToList();
            var options = await db.Set<ModifierOption>().AsNoTracking()
                .Where(x => groupIds.Contains(x.ModifierGroupId))
                .OrderBy(x => x.SortOrder).ToListAsync(ct);
            var result = groups.Select(g => new
            {
                g.Id, g.Name, g.MinSelect, g.MaxSelect, g.IsRequired,
                options = options.Where(o => o.ModifierGroupId == g.Id),
            });
            return Results.Ok(result);
        }).RequireAuthorization();

        endpoints.MapPatch("/modifier-groups/{groupId:guid}", async (
            Guid groupId, UpdateModifierGroupDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var group = await guard.LoadModifierGroupAsManagerAsync(principal, groupId, ct);
            if (dto.Name is not null)
            {
                MenuEndpoints.ValidateName(dto.Name, "Tên nhóm tùy chọn");
                group.Name = dto.Name.Trim();
            }
            var min = dto.MinSelect ?? group.MinSelect;
            var max = dto.MaxSelect ?? group.MaxSelect;
            ValidateSelectRange(min, max);
            group.MinSelect = min;
            group.MaxSelect = max;
            group.IsRequired = dto.IsRequired ?? group.IsRequired;
            await db.SaveChangesAsync(ct);
            return Results.Ok(group);
        }).RequireAuthorization();

        endpoints.MapPost("/modifier-groups/{groupId:guid}/options", async (
            Guid groupId, CreateModifierOptionDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var group = await guard.LoadModifierGroupAsManagerAsync(principal, groupId, ct);
            MenuEndpoints.ValidateName(dto.Name, "Tên tùy chọn");
            var option = new ModifierOption
            {
                ModifierGroupId = group.Id,
                Name = dto.Name.Trim(),
                PriceDeltaMinor = dto.PriceDeltaMinor,
                SortOrder = dto.SortOrder ?? 0,
            };
            db.Add(option);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/modifier-options/{option.Id}", option);
        }).RequireAuthorization();

        endpoints.MapPatch("/modifier-options/{optionId:guid}", async (
            Guid optionId, UpdateModifierOptionDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var option = await db.Set<ModifierOption>().FirstOrDefaultAsync(x => x.Id == optionId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy tùy chọn.", 404);
            await guard.LoadModifierGroupAsManagerAsync(principal, option.ModifierGroupId, ct);
            if (dto.Name is not null)
            {
                MenuEndpoints.ValidateName(dto.Name, "Tên tùy chọn");
                option.Name = dto.Name.Trim();
            }
            option.PriceDeltaMinor = dto.PriceDeltaMinor ?? option.PriceDeltaMinor;
            option.IsAvailable = dto.IsAvailable ?? option.IsAvailable;
            option.SortOrder = dto.SortOrder ?? option.SortOrder;
            await db.SaveChangesAsync(ct);
            return Results.Ok(option);
        }).RequireAuthorization();

        // Gán danh sách nhóm tùy chọn cho món (thay toàn bộ).
        endpoints.MapPut("/products/{productId:guid}/modifier-groups", async (
            Guid productId, AssignModifierGroupsDto dto, ClaimsPrincipal principal,
            TingGoDbContext db, CatalogGuard guard, CancellationToken ct) =>
        {
            var product = await guard.LoadProductAsManagerAsync(principal, productId, ct);

            var validGroupCount = await db.Set<ModifierGroup>()
                .CountAsync(x => dto.ModifierGroupIds.Contains(x.Id) && x.VenueId == product.VenueId, ct);
            if (validGroupCount != dto.ModifierGroupIds.Distinct().Count())
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Có nhóm tùy chọn không tồn tại hoặc không thuộc quán này.", 400);
            }

            var existing = await db.Set<ProductModifierGroup>()
                .Where(x => x.ProductId == productId).ToListAsync(ct);
            db.RemoveRange(existing);
            var assignments = dto.ModifierGroupIds
                .Select((groupId, index) => new ProductModifierGroup
                {
                    ProductId = productId,
                    ModifierGroupId = groupId,
                    SortOrder = index,
                });
            db.AddRange(assignments);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static void ValidateSelectRange(int min, int max)
    {
        if (min < 0 || max < 1 || min > max)
        {
            throw new ApiException(ErrorCodes.ValidationFailed,
                "minSelect/maxSelect không hợp lệ (0 ≤ min ≤ max, max ≥ 1).", 400);
        }
    }
}
