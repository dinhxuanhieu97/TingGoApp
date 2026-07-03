using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Venues.Domain;
using TingGo.Modules.Venues.Persistence;
using TingGo.Modules.Venues.Services;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Venues;

public sealed record CreateOrganizationDto(string Name, string? DefaultLocale, string? DefaultCurrency);
public sealed record CreateVenueDto(
    string Name, string? Slug, string? CountryCode, string? Timezone,
    string? DefaultLocale, string? CurrencyCode);
public sealed record UpdateOrganizationDto(string? Name, string? DefaultLocale, string? DefaultCurrency);
public sealed record UpdateVenueDto(
    string? Name, string? Timezone, string? DefaultLocale, string? CurrencyCode,
    string? WifiName, long RowVersion);
public sealed record UpdateVenueStatusDto(string Status, long RowVersion);

public sealed class VenuesModule : IModule
{
    public string Name => "Venues";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, VenuesEntityConfigurator>();
        services.AddScoped<IVenueDirectory, VenueDirectory>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // MER-01: Onboarding — tạo tổ chức, user hiện tại thành owner.
        endpoints.MapPost("/organizations", async (
            CreateOrganizationDto dto,
            ClaimsPrincipal principal,
            TingGoDbContext db,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Tên tổ chức không hợp lệ.", 400);
            }

            var userId = GetUserId(principal);
            var org = new Organization
            {
                Name = dto.Name.Trim(),
                DefaultLocale = dto.DefaultLocale ?? "vi-VN",
                DefaultCurrency = dto.DefaultCurrency ?? "VND",
            };
            db.Add(org);
            await db.SaveChangesAsync(ct);
            await memberships.CreateOwnerMembershipAsync(userId, org.Id, ct);

            return Results.Created($"/api/v1/organizations/{org.Id}", ToDto(org));
        }).RequireAuthorization();

        endpoints.MapGet("/organizations/{id:guid}", async (
            Guid id, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureMemberAsync(memberships, principal, id, ct);
            var org = await db.Set<Organization>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy tổ chức.", 404);
            return Results.Ok(ToDto(org));
        }).RequireAuthorization();

        endpoints.MapGet("/organizations/{id:guid}/venues", async (
            Guid id, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureMemberAsync(memberships, principal, id, ct);
            var items = await db.Set<Venue>().AsNoTracking()
                .Where(x => x.OrganizationId == id)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
            return Results.Ok(items.Select(ToDto));
        }).RequireAuthorization();

        endpoints.MapPost("/organizations/{id:guid}/venues", async (
            Guid id, CreateVenueDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            var role = await EnsureMemberAsync(memberships, principal, id, ct);
            if (role != "owner" && role != "manager")
            {
                throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được tạo quán.", 403);
            }
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Tên quán không hợp lệ.", 400);
            }

            var org = await db.Set<Organization>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy tổ chức.", 404);

            var slug = await GenerateUniqueSlugAsync(db, dto.Slug ?? dto.Name, ct);
            var venue = new Venue
            {
                OrganizationId = org.Id,
                Name = dto.Name.Trim(),
                Slug = slug,
                CountryCode = dto.CountryCode ?? "VN",
                Timezone = dto.Timezone ?? "Asia/Ho_Chi_Minh",
                DefaultLocale = dto.DefaultLocale ?? org.DefaultLocale,
                CurrencyCode = dto.CurrencyCode ?? org.DefaultCurrency,
            };
            db.Add(venue);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/api/v1/venues/{venue.Id}", ToDto(venue));
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            var venue = await db.Set<Venue>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == venueId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
            await EnsureMemberAsync(memberships, principal, venue.OrganizationId, ct);
            return Results.Ok(ToDto(venue));
        }).RequireAuthorization();

        Endpoints.TableEndpoints.Map(endpoints);
        Endpoints.PublicQrEndpoints.Map(endpoints);

        endpoints.MapPatch("/organizations/{id:guid}", async (
            Guid id, UpdateOrganizationDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            var role = await EnsureMemberAsync(memberships, principal, id, ct);
            if (role != "owner")
            {
                throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner được sửa tổ chức.", 403);
            }

            var org = await db.Set<Organization>().FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy tổ chức.", 404);

            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Tên tổ chức không hợp lệ.", 400);
                }
                org.Name = dto.Name.Trim();
            }
            org.DefaultLocale = dto.DefaultLocale ?? org.DefaultLocale;
            org.DefaultCurrency = dto.DefaultCurrency ?? org.DefaultCurrency;
            org.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(org));
        }).RequireAuthorization();

        endpoints.MapPatch("/venues/{venueId:guid}", async (
            Guid venueId, UpdateVenueDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            var venue = await LoadVenueForManagerAsync(db, memberships, principal, venueId, dto.RowVersion, ct);

            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 200)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Tên quán không hợp lệ.", 400);
                }
                venue.Name = dto.Name.Trim();
            }
            venue.Timezone = dto.Timezone ?? venue.Timezone;
            venue.DefaultLocale = dto.DefaultLocale ?? venue.DefaultLocale;
            venue.CurrencyCode = dto.CurrencyCode ?? venue.CurrencyCode;
            venue.WifiName = dto.WifiName ?? venue.WifiName;
            Touch(venue);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(venue));
        }).RequireAuthorization();

        endpoints.MapPatch("/venues/{venueId:guid}/status", async (
            Guid venueId, UpdateVenueStatusDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, CancellationToken ct) =>
        {
            if (dto.Status is not ("active" or "inactive"))
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Status phải là active hoặc inactive.", 400);
            }
            var venue = await LoadVenueForManagerAsync(db, memberships, principal, venueId, dto.RowVersion, ct);
            venue.Status = dto.Status;
            Touch(venue);
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToDto(venue));
        }).RequireAuthorization();

        return endpoints;
    }

    /// <summary>Load venue + check quyền owner/manager + optimistic concurrency theo rowVersion.</summary>
    private static async Task<Venue> LoadVenueForManagerAsync(
        TingGoDbContext db, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, long rowVersion, CancellationToken ct)
    {
        var venue = await db.Set<Venue>().FirstOrDefaultAsync(x => x.Id == venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);

        var role = await memberships.GetOrganizationRoleAsync(
            GetUserId(principal), venue.OrganizationId, ct);
        if (role is not ("owner" or "manager"))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được sửa quán.", 403);
        }

        if (venue.RowVersion != rowVersion)
        {
            throw new ApiException(ErrorCodes.Conflict,
                "Dữ liệu quán đã thay đổi. Hãy tải lại và thử lại.", 409,
                new Dictionary<string, object?> { ["currentRowVersion"] = venue.RowVersion });
        }
        return venue;
    }

    private static void Touch(Venue venue)
    {
        venue.RowVersion++;
        venue.UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Tenant isolation: user phải có membership active trong organization.</summary>
    private static async Task<string> EnsureMemberAsync(
        IMembershipService memberships, ClaimsPrincipal principal, Guid organizationId, CancellationToken ct)
    {
        var role = await memberships.GetOrganizationRoleAsync(GetUserId(principal), organizationId, ct);
        return role ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không có quyền truy cập tổ chức này.", 403);
    }

    private static async Task<string> GenerateUniqueSlugAsync(TingGoDbContext db, string source, CancellationToken ct)
    {
        var baseSlug = Slugify(source);
        var slug = baseSlug;
        var i = 1;
        while (await db.Set<Venue>().AnyAsync(x => x.Slug == slug, ct))
        {
            slug = $"{baseSlug}-{++i}";
        }
        return slug;
    }

    private static string Slugify(string value)
    {
        var normalized = value.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var category = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(c)) sb.Append(c);
            else if (c is ' ' or '-' or '_') sb.Append('-');
            // 'đ' không phân rã qua FormD nên xử lý riêng
            else if (c == 'đ') sb.Append('d');
        }
        var slug = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? $"quan-{Guid.NewGuid().ToString("N")[..6]}" : slug[..Math.Min(slug.Length, 100)];
    }

    private static object ToDto(Organization org)
        => new { org.Id, org.Name, org.Status, org.DefaultLocale, org.DefaultCurrency, org.CreatedAt };

    private static object ToDto(Venue v)
        => new
        {
            v.Id, v.OrganizationId, v.Name, v.Slug, v.CountryCode, v.Timezone,
            v.DefaultLocale, v.CurrencyCode, v.Status, v.RowVersion, v.CreatedAt,
        };

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return Guid.Parse(sub);
    }
}
