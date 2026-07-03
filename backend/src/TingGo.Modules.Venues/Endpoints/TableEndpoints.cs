using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Venues.Endpoints;

public sealed record CreateAreaDto(string Name, int? SortOrder);
public sealed record UpdateAreaDto(string? Name, int? SortOrder);
public sealed record CreateTableDto(Guid AreaId, string? Code, string Name);
public sealed record BulkCreateTablesDto(Guid AreaId, int Count, string? Prefix);
public sealed record UpdateTableDto(string? Name, Guid? AreaId);

public static class TableEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // --- Khu vực ---
        endpoints.MapGet("/venues/{venueId:guid}/areas", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            await EnsureStaffAsync(memberships, venues, principal, venueId, ct);
            var items = await db.Set<VenueArea>().AsNoTracking()
                .Where(x => x.VenueId == venueId).OrderBy(x => x.SortOrder).ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/areas", async (
            Guid venueId, CreateAreaDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            await EnsureManagerAsync(memberships, venues, principal, venueId, ct);
            if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 100)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Tên khu vực không hợp lệ.", 400);
            }
            var area = new VenueArea { VenueId = venueId, Name = dto.Name.Trim(), SortOrder = dto.SortOrder ?? 0 };
            db.Add(area);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/areas/{area.Id}", area);
        }).RequireAuthorization();

        endpoints.MapPatch("/areas/{areaId:guid}", async (
            Guid areaId, UpdateAreaDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            var area = await db.Set<VenueArea>().FirstOrDefaultAsync(x => x.Id == areaId, ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy khu vực.", 404);
            await EnsureManagerAsync(memberships, venues, principal, area.VenueId, ct);
            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 100)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Tên khu vực không hợp lệ.", 400);
                }
                area.Name = dto.Name.Trim();
            }
            area.SortOrder = dto.SortOrder ?? area.SortOrder;
            await db.SaveChangesAsync(ct);
            return Results.Ok(area);
        }).RequireAuthorization();

        // --- Bàn ---
        endpoints.MapGet("/venues/{venueId:guid}/tables", async (
            Guid venueId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            await EnsureStaffAsync(memberships, venues, principal, venueId, ct);
            var items = await db.Set<DiningTable>().AsNoTracking()
                .Where(x => x.VenueId == venueId)
                .OrderBy(x => x.Code)
                .Select(t => new
                {
                    t.Id, t.AreaId, t.Code, t.Name, t.Status, t.CreatedAt,
                    hasActiveQr = db.Set<QrCode>().Any(q => q.TableId == t.Id && q.Status == QrStatus.Active),
                })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/tables", async (
            Guid venueId, CreateTableDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues,
            IConfiguration configuration, CancellationToken ct) =>
        {
            await EnsureManagerAsync(memberships, venues, principal, venueId, ct);
            var created = await CreateTablesAsync(db, configuration, venueId, dto.AreaId,
                [(dto.Code, dto.Name)], ct);
            return Results.Created($"/api/v1/tables/{created[0].Id}", created[0]);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/tables/bulk", async (
            Guid venueId, BulkCreateTablesDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues,
            IConfiguration configuration, CancellationToken ct) =>
        {
            await EnsureManagerAsync(memberships, venues, principal, venueId, ct);
            if (dto.Count is < 1 or > 100)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Số bàn phải từ 1 đến 100.", 400);
            }
            var prefix = string.IsNullOrWhiteSpace(dto.Prefix) ? "T" : dto.Prefix.Trim();
            var existingCodes = await db.Set<DiningTable>()
                .Where(x => x.VenueId == venueId).Select(x => x.Code).ToListAsync(ct);
            var taken = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var specs = new List<(string? Code, string Name)>();
            var n = 1;
            while (specs.Count < dto.Count)
            {
                var code = $"{prefix}{n:D2}";
                if (!taken.Contains(code)) specs.Add((code, $"Bàn {n}"));
                n++;
            }
            var created = await CreateTablesAsync(db, configuration, venueId, dto.AreaId, specs, ct);
            return Results.Ok(created);
        }).RequireAuthorization();

        endpoints.MapPatch("/tables/{tableId:guid}", async (
            Guid tableId, UpdateTableDto dto, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            var table = await LoadTableAsync(db, tableId, ct);
            await EnsureManagerAsync(memberships, venues, principal, table.VenueId, ct);
            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Length > 100)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Tên bàn không hợp lệ.", 400);
                }
                table.Name = dto.Name.Trim();
            }
            if (dto.AreaId is not null)
            {
                var areaOk = await db.Set<VenueArea>()
                    .AnyAsync(x => x.Id == dto.AreaId && x.VenueId == table.VenueId, ct);
                if (!areaOk)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Khu vực không thuộc quán này.", 400);
                }
                table.AreaId = dto.AreaId.Value;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(table);
        }).RequireAuthorization();

        endpoints.MapPost("/tables/{tableId:guid}/disable", async (
            Guid tableId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            var table = await LoadTableAsync(db, tableId, ct);
            await EnsureManagerAsync(memberships, venues, principal, table.VenueId, ct);
            table.Status = table.Status == TableStatus.Active ? TableStatus.Disabled : TableStatus.Active;
            await db.SaveChangesAsync(ct);
            return Results.Ok(table);
        }).RequireAuthorization();

        endpoints.MapPost("/tables/{tableId:guid}/qr/regenerate", async (
            Guid tableId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues,
            IConfiguration configuration, CancellationToken ct) =>
        {
            var table = await LoadTableAsync(db, tableId, ct);
            await EnsureManagerAsync(memberships, venues, principal, table.VenueId, ct);

            await db.Set<QrCode>()
                .Where(x => x.TableId == tableId && x.Status == QrStatus.Active)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, QrStatus.Revoked), ct);

            var (qr, rawToken) = NewQr(tableId);
            db.Add(qr);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { table.Id, table.Code, qrUrl = QrUrl(configuration, rawToken), rawToken });
        }).RequireAuthorization();

        endpoints.MapGet("/tables/{tableId:guid}/qr", async (
            Guid tableId, ClaimsPrincipal principal, TingGoDbContext db,
            IMembershipService memberships, IVenueDirectory venues, CancellationToken ct) =>
        {
            var table = await LoadTableAsync(db, tableId, ct);
            await EnsureStaffAsync(memberships, venues, principal, table.VenueId, ct);
            var qr = await db.Set<QrCode>().AsNoTracking()
                .Where(x => x.TableId == tableId && x.Status == QrStatus.Active)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.Status, x.CreatedAt })
                .FirstOrDefaultAsync(ct);
            // Raw token không lưu — muốn in lại QR dùng /qr/regenerate.
            return Results.Ok(new { table.Id, table.Code, qr });
        }).RequireAuthorization();
    }

    private static async Task<List<TableCreated>> CreateTablesAsync(
        TingGoDbContext db, IConfiguration configuration, Guid venueId, Guid areaId,
        IReadOnlyList<(string? Code, string Name)> specs, CancellationToken ct)
    {
        var areaOk = await db.Set<VenueArea>().AnyAsync(x => x.Id == areaId && x.VenueId == venueId, ct);
        if (!areaOk)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Khu vực không thuộc quán này.", 400);
        }

        var result = new List<TableCreated>();
        foreach (var (code, name) in specs)
        {
            var tableCode = string.IsNullOrWhiteSpace(code)
                ? $"T{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
                : code.Trim().ToUpperInvariant();
            var exists = await db.Set<DiningTable>()
                .AnyAsync(x => x.VenueId == venueId && x.Code == tableCode, ct);
            if (exists)
            {
                throw new ApiException(ErrorCodes.Conflict, $"Mã bàn {tableCode} đã tồn tại.", 409);
            }

            var table = new DiningTable { VenueId = venueId, AreaId = areaId, Code = tableCode, Name = name };
            var (qr, rawToken) = NewQr(table.Id);
            db.Add(table);
            db.Add(qr);
            result.Add(new TableCreated(table.Id, table.AreaId, table.Code, table.Name,
                QrUrl(configuration, rawToken), rawToken));
        }
        await db.SaveChangesAsync(ct);
        return result;
    }

    private static (QrCode Qr, string RawToken) NewQr(Guid tableId)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return (new QrCode { TableId = tableId, TokenHash = Sha256(rawToken) }, rawToken);
    }

    internal static string Sha256(string value)
        => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    private static string QrUrl(IConfiguration configuration, string rawToken)
        => $"{configuration["PublicWeb:BaseUrl"] ?? "http://localhost:3001"}/q/{rawToken}";

    private static async Task<DiningTable> LoadTableAsync(TingGoDbContext db, Guid tableId, CancellationToken ct)
        => await db.Set<DiningTable>().FirstOrDefaultAsync(x => x.Id == tableId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy bàn.", 404);

    private static async Task EnsureManagerAsync(
        IMembershipService memberships, IVenueDirectory venues, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var role = await RoleAsync(memberships, venues, principal, venueId, ct);
        if (role is not ("owner" or "manager"))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được quản lý bàn.", 403);
        }
    }

    private static async Task EnsureStaffAsync(
        IMembershipService memberships, IVenueDirectory venues, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        _ = await RoleAsync(memberships, venues, principal, venueId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
    }

    private static async Task<string?> RoleAsync(
        IMembershipService memberships, IVenueDirectory venues, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var organizationId = await venues.GetOrganizationIdAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return await memberships.GetOrganizationRoleAsync(Guid.Parse(sub), organizationId, ct);
    }

    public sealed record TableCreated(Guid Id, Guid AreaId, string Code, string Name, string QrUrl, string RawToken);
}
