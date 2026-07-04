using System.Security.Claims;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Endpoints;

/// <summary>
/// Import dữ liệu quán từ Excel — onboard nhanh thay vì nhập tay.
/// File 2 sheet: "Mon" (danh mục, tên, giá, mô tả, ảnh, size, topping) và "Ban" (khu vực, mã, tên).
/// Đặt ở host vì orchestrate chéo Catalog + Venues.
/// </summary>
public static class ImportEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // File mẫu kèm dữ liệu ví dụ + hướng dẫn
        endpoints.MapGet("/venues/{venueId:guid}/import/template", async (
            Guid venueId, ClaimsPrincipal principal,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureManagerAsync(venues, memberships, principal, venueId, ct);

            using var workbook = new XLWorkbook();
            var menuSheet = workbook.Worksheets.Add("Mon");
            menuSheet.Cell(1, 1).Value = "Danh mục";
            menuSheet.Cell(1, 2).Value = "Tên món";
            menuSheet.Cell(1, 3).Value = "Giá (đồng)";
            menuSheet.Cell(1, 4).Value = "Mô tả";
            menuSheet.Cell(1, 5).Value = "Ảnh (URL)";
            menuSheet.Cell(1, 6).Value = "Size (Tên:phụ thu; ...)";
            menuSheet.Cell(1, 7).Value = "Topping (Tên:giá; ...)";
            menuSheet.Row(1).Style.Font.Bold = true;
            menuSheet.Cell(2, 1).Value = "Cà phê";
            menuSheet.Cell(2, 2).Value = "Cà phê sữa đá";
            menuSheet.Cell(2, 3).Value = 29000;
            menuSheet.Cell(2, 4).Value = "Cà phê phin truyền thống pha sữa đặc";
            menuSheet.Cell(2, 6).Value = "M:0; L:6000";
            menuSheet.Cell(3, 1).Value = "Trà sữa";
            menuSheet.Cell(3, 2).Value = "Trà sữa trân châu";
            menuSheet.Cell(3, 3).Value = 39000;
            menuSheet.Cell(3, 7).Value = "Trân châu:7000; Thạch dừa:5000";
            menuSheet.Columns().AdjustToContents();

            var tableSheet = workbook.Worksheets.Add("Ban");
            tableSheet.Cell(1, 1).Value = "Khu vực";
            tableSheet.Cell(1, 2).Value = "Mã bàn";
            tableSheet.Cell(1, 3).Value = "Tên bàn";
            tableSheet.Row(1).Style.Font.Bold = true;
            tableSheet.Cell(2, 1).Value = "Tầng 1";
            tableSheet.Cell(2, 2).Value = "T01";
            tableSheet.Cell(2, 3).Value = "Bàn 1";
            tableSheet.Cell(3, 1).Value = "Tầng 1";
            tableSheet.Cell(3, 2).Value = "T02";
            tableSheet.Cell(3, 3).Value = "Bàn 2";
            tableSheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Results.File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "tinggo-mau-nhap-lieu.xlsx");
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/import", async (
            Guid venueId, IFormFile file, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var venueInfo = await EnsureManagerAsync(venues, memberships, principal, venueId, ct);
            if (file.Length is 0 or > 10 * 1024 * 1024)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "File phải nhỏ hơn 10 MB.", 400);
            }

            XLWorkbook workbook;
            try
            {
                workbook = new XLWorkbook(file.OpenReadStream());
            }
            catch
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Không đọc được file. Hãy dùng file Excel (.xlsx) theo mẫu.", 400);
            }

            var errors = new List<string>();
            int categoriesCreated = 0, productsCreated = 0, productsSkipped = 0;
            int areasCreated = 0, tablesCreated = 0, tablesSkipped = 0;
            var newTables = new List<object>();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // --- Menu: lấy menu đầu tiên hoặc tạo mới ---
            var menu = await db.Set<Menu>().FirstOrDefaultAsync(x => x.VenueId == venueId, ct);
            var menuCreated = menu is null;
            if (menu is null)
            {
                menu = new Menu { VenueId = venueId, Name = "Menu chính" };
                db.Add(menu);
            }

            var categories = await db.Set<MenuCategory>()
                .Where(x => x.MenuId == menu.Id).ToListAsync(ct);
            var existingProducts = await db.Set<Product>()
                .Where(x => x.VenueId == venueId && x.Status == ProductStatus.Active)
                .Select(x => new { x.CategoryId, x.Name })
                .ToListAsync(ct);
            var productKeys = existingProducts
                .Select(x => $"{x.CategoryId}|{x.Name.ToLowerInvariant()}").ToHashSet();

            // --- Sheet "Mon" ---
            if (workbook.Worksheets.TryGetWorksheet("Mon", out var menuSheet))
            {
                foreach (var row in menuSheet.RowsUsed().Skip(1))
                {
                    var rowNumber = row.RowNumber();
                    var categoryName = row.Cell(1).GetString().Trim();
                    var productName = row.Cell(2).GetString().Trim();
                    if (categoryName.Length == 0 && productName.Length == 0) continue;
                    if (categoryName.Length == 0 || productName.Length == 0)
                    {
                        errors.Add($"Mon dòng {rowNumber}: thiếu danh mục hoặc tên món.");
                        continue;
                    }
                    if (!row.Cell(3).TryGetValue<long>(out var price) || price < 0)
                    {
                        errors.Add($"Mon dòng {rowNumber}: giá không hợp lệ.");
                        continue;
                    }

                    var category = categories.FirstOrDefault(
                        c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
                    if (category is null)
                    {
                        category = new MenuCategory
                        {
                            MenuId = menu.Id, Name = categoryName, SortOrder = categories.Count + 1,
                        };
                        categories.Add(category);
                        db.Add(category);
                        categoriesCreated++;
                    }

                    if (!productKeys.Add($"{category.Id}|{productName.ToLowerInvariant()}"))
                    {
                        productsSkipped++;
                        continue;
                    }

                    var imageUrl = row.Cell(5).GetString().Trim();
                    var product = new Product
                    {
                        VenueId = venueId,
                        CategoryId = category.Id,
                        Name = productName,
                        Description = NullIfEmpty(row.Cell(4).GetString().Trim()),
                        BasePriceMinor = price,
                        CurrencyCode = venueInfo.CurrencyCode,
                        ImageUrl = imageUrl.StartsWith("http") ? imageUrl : null,
                        SortOrder = productsCreated,
                    };
                    db.Add(product);
                    productsCreated++;

                    // Size: "M:0; L:6000"
                    foreach (var (name, delta, index) in ParsePairs(row.Cell(6).GetString()))
                    {
                        db.Add(new ProductVariant
                        {
                            ProductId = product.Id, Name = name,
                            PriceDeltaMinor = delta, IsDefault = index == 0,
                        });
                    }

                    // Topping: nhóm riêng cho món
                    var toppings = ParsePairs(row.Cell(7).GetString()).ToList();
                    if (toppings.Count > 0)
                    {
                        var group = new ModifierGroup
                        {
                            VenueId = venueId, Name = $"Topping — {productName}",
                            MinSelect = 0, MaxSelect = toppings.Count,
                        };
                        db.Add(group);
                        db.Add(new ProductModifierGroup { ProductId = product.Id, ModifierGroupId = group.Id });
                        foreach (var (name, delta, index) in toppings)
                        {
                            db.Add(new ModifierOption
                            {
                                ModifierGroupId = group.Id, Name = name,
                                PriceDeltaMinor = delta, SortOrder = index,
                            });
                        }
                    }
                }
            }

            // --- Sheet "Ban" ---
            if (workbook.Worksheets.TryGetWorksheet("Ban", out var tableSheet))
            {
                var areas = await db.Set<VenueArea>().Where(x => x.VenueId == venueId).ToListAsync(ct);
                var tableCodes = (await db.Set<DiningTable>()
                        .Where(x => x.VenueId == venueId).Select(x => x.Code).ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var row in tableSheet.RowsUsed().Skip(1))
                {
                    var rowNumber = row.RowNumber();
                    var areaName = row.Cell(1).GetString().Trim();
                    var code = row.Cell(2).GetString().Trim().ToUpperInvariant();
                    var tableName = row.Cell(3).GetString().Trim();
                    if (areaName.Length == 0 && code.Length == 0) continue;
                    if (areaName.Length == 0 || code.Length == 0)
                    {
                        errors.Add($"Ban dòng {rowNumber}: thiếu khu vực hoặc mã bàn.");
                        continue;
                    }

                    var area = areas.FirstOrDefault(
                        a => a.Name.Equals(areaName, StringComparison.OrdinalIgnoreCase));
                    if (area is null)
                    {
                        area = new VenueArea { VenueId = venueId, Name = areaName, SortOrder = areas.Count };
                        areas.Add(area);
                        db.Add(area);
                        areasCreated++;
                    }

                    if (!tableCodes.Add(code))
                    {
                        tablesSkipped++;
                        continue;
                    }

                    var table = new DiningTable
                    {
                        VenueId = venueId, AreaId = area.Id, Code = code,
                        Name = tableName.Length > 0 ? tableName : code,
                    };
                    var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
                    db.Add(table);
                    db.Add(new QrCode
                    {
                        TableId = table.Id,
                        TokenHash = Convert.ToHexString(
                            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))),
                    });
                    tablesCreated++;
                    newTables.Add(new { table.Id, table.Code, table.Name, area = area.Name, rawToken });
                }
            }

            // Menu mới tạo + có danh mục → tự công bố
            if (menuCreated && categories.Count > 0)
            {
                menu.Status = MenuStatus.Published;
                menu.PublishedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Results.Ok(new
            {
                menuCreated,
                menuPublished = menu.Status == MenuStatus.Published,
                categoriesCreated, productsCreated, productsSkipped,
                areasCreated, tablesCreated, tablesSkipped,
                newTables, errors,
            });
        }).RequireAuthorization().DisableAntiforgery();
    }

    private static IEnumerable<(string Name, long Delta, int Index)> ParsePairs(string raw)
    {
        // "M:0; L:6000" → [(M,0,0),(L,6000,1)] — bỏ qua phần tử hỏng
        var index = 0;
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = part.Split(':', 2, StringSplitOptions.TrimEntries);
            if (pieces.Length == 0 || pieces[0].Length == 0) continue;
            long delta = 0;
            if (pieces.Length == 2 && !long.TryParse(pieces[1].Replace(".", "").Replace(",", ""), out delta))
            {
                continue;
            }
            yield return (pieces[0], delta, index++);
        }
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    private static async Task<VenueInfo> EnsureManagerAsync(
        IVenueDirectory venues, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var venueInfo = await venues.GetVenueInfoAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        var role = await memberships.GetOrganizationRoleAsync(Guid.Parse(sub), venueInfo.OrganizationId, ct);
        if (role is not ("owner" or "manager"))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được import dữ liệu.", 403);
        }
        return venueInfo;
    }
}
