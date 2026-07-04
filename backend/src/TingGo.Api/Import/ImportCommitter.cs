using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.Modules.Catalog.Services;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Import;

/// <summary>
/// Ghi staging rows vào DB chính trong MỘT transaction (AC 7).
/// QR tạo cho bàn active (AC 9); menu tạo ở trạng thái NHÁP (AC 10).
/// </summary>
public static class ImportCommitter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record CommitOutcome(
        int AreasCreated, int TablesCreated, int CategoriesCreated, int ProductsCreated,
        int VariantsCreated, int GroupsCreated, int OptionsCreated, int LinksCreated,
        bool MenuCreated, int ImagesAttached, int ImagesFailed, List<object> NewTables);

    public static async Task<CommitOutcome> CommitAsync(
        TingGoDbContext db, ImportJob job, Guid actorMembershipId, string currencyCode,
        IImageStorage imageStorage, CancellationToken ct)
    {
        var rows = await db.Set<ImportRow>().AsNoTracking()
            .Where(x => x.ImportJobId == job.Id && x.RowStatus != "error")
            .ToListAsync(ct);

        List<T> Section<T>(string section) => rows
            .Where(r => r.SectionType == section)
            .Select(r => JsonSerializer.Deserialize<T>(r.NormalizedData, Json)!)
            .ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Venue settings (chỉ các field hỗ trợ)
        var venueRow = Section<VenueRowData>(ImportSections.Venue).FirstOrDefault();
        if (venueRow is not null)
        {
            var venue = await db.Set<Venue>().FirstAsync(x => x.Id == job.VenueId, ct);
            venue.WifiName = venueRow.WifiName ?? venue.WifiName;
            venue.DefaultLocale = venueRow.DefaultLocale ?? venue.DefaultLocale;
            venue.CurrencyCode = venueRow.CurrencyCode ?? venue.CurrencyCode;
            venue.Timezone = venueRow.Timezone ?? venue.Timezone;
            venue.RowVersion++;
            venue.UpdatedAt = DateTimeOffset.UtcNow;
            currencyCode = venue.CurrencyCode;
        }

        // Areas
        var areaByCode = new Dictionary<string, VenueArea>(StringComparer.OrdinalIgnoreCase);
        var existingAreaCount = await db.Set<VenueArea>().CountAsync(a => a.VenueId == job.VenueId, ct);
        foreach (var data in Section<AreaRowData>(ImportSections.Areas).OrderBy(a => a.SortOrder))
        {
            var area = new VenueArea
            {
                VenueId = job.VenueId, Name = data.Name,
                SortOrder = data.SortOrder > 0 ? data.SortOrder : existingAreaCount + areaByCode.Count,
            };
            db.Add(area);
            areaByCode[data.Code] = area;
        }

        // Tables + QR (chỉ bàn active mới có QR — AC 9)
        var newTables = new List<object>();
        foreach (var data in Section<TableRowData>(ImportSections.Tables).OrderBy(t => t.SortOrder))
        {
            var table = new DiningTable
            {
                VenueId = job.VenueId,
                AreaId = areaByCode[data.AreaCode].Id,
                Code = data.Code,
                Name = data.Name,
                Status = data.IsActive ? TableStatus.Active : TableStatus.Disabled,
            };
            db.Add(table);
            string? rawToken = null;
            if (data.IsActive)
            {
                rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
                    .Replace('+', '-').Replace('/', '_').TrimEnd('=');
                db.Add(new QrCode
                {
                    TableId = table.Id,
                    TokenHash = Convert.ToHexString(
                        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken))),
                });
            }
            newTables.Add(new { table.Id, table.Code, table.Name, area = areaByCode[data.AreaCode].Name, rawToken });
        }

        // Menu: dùng menu đầu tiên nếu có, không thì tạo NHÁP (AC 10 — không tự công bố)
        var categoryRows = Section<CategoryRowData>(ImportSections.Categories);
        var menu = await db.Set<Menu>().FirstOrDefaultAsync(x => x.VenueId == job.VenueId, ct);
        var menuCreated = false;
        if (menu is null && categoryRows.Count > 0)
        {
            menu = new Menu { VenueId = job.VenueId, Name = "Menu chính" }; // status mặc định: draft
            db.Add(menu);
            menuCreated = true;
        }

        var categoryByCode = new Dictionary<string, MenuCategory>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in categoryRows.OrderBy(c => c.SortOrder))
        {
            var category = new MenuCategory
            {
                MenuId = menu!.Id, Name = data.Name,
                SortOrder = data.SortOrder, IsVisible = data.IsVisible,
            };
            db.Add(category);
            categoryByCode[data.Code] = category;
        }

        // Products (sku = product_code)
        var productByCode = new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in Section<ProductRowData>(ImportSections.Products).OrderBy(p => p.SortOrder))
        {
            var product = new Product
            {
                VenueId = job.VenueId,
                CategoryId = categoryByCode[data.CategoryCode].Id,
                Sku = data.Code,
                Name = data.Name,
                Description = data.Description,
                BasePriceMinor = data.BasePriceMinor,
                CurrencyCode = currencyCode,
                // URL http giữ nguyên; tên file trong ZIP gắn SAU transaction (PRD 7.3)
                ImageUrl = data.ImageFile?.StartsWith("http") == true ? data.ImageFile : null,
                IsAvailable = data.IsAvailable,
                SortOrder = data.SortOrder,
            };
            db.Add(product);
            productByCode[data.Code] = product;
        }

        // Variants
        var variantsCreated = 0;
        foreach (var data in Section<VariantRowData>(ImportSections.Variants).OrderBy(v => v.SortOrder))
        {
            db.Add(new ProductVariant
            {
                ProductId = productByCode[data.ProductCode].Id,
                Name = data.Name, PriceDeltaMinor = data.PriceDeltaMinor,
                IsDefault = data.IsDefault, IsAvailable = data.IsAvailable,
            });
            variantsCreated++;
        }

        // Modifier groups + options + links
        var groupByCode = new Dictionary<string, ModifierGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var data in Section<GroupRowData>(ImportSections.ModifierGroups).OrderBy(g => g.SortOrder))
        {
            var group = new ModifierGroup
            {
                VenueId = job.VenueId, Name = data.Name,
                MinSelect = data.MinSelect, MaxSelect = data.MaxSelect, IsRequired = data.IsRequired,
            };
            db.Add(group);
            groupByCode[data.Code] = group;
        }
        var optionsCreated = 0;
        foreach (var data in Section<OptionRowData>(ImportSections.ModifierOptions).OrderBy(o => o.SortOrder))
        {
            db.Add(new ModifierOption
            {
                ModifierGroupId = groupByCode[data.GroupCode].Id,
                Name = data.Name, PriceDeltaMinor = data.PriceDeltaMinor,
                IsAvailable = data.IsAvailable, SortOrder = data.SortOrder,
            });
            optionsCreated++;
        }
        var linksCreated = 0;
        foreach (var data in Section<ProductModifierRowData>(ImportSections.ProductModifiers))
        {
            db.Add(new ProductModifierGroup
            {
                ProductId = productByCode[data.ProductCode].Id,
                ModifierGroupId = groupByCode[data.GroupCode].Id,
                SortOrder = data.SortOrder,
            });
            linksCreated++;
        }

        // Audit log (AC 11)
        db.Add(new AuditLog
        {
            VenueId = job.VenueId, ActorUserId = actorMembershipId,
            Action = "import.commit", EntityType = "ImportJob", EntityId = job.Id,
            Detail = $"areas={areaByCode.Count} tables={newTables.Count} categories={categoryByCode.Count} products={productByCode.Count}",
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race với dữ liệu tạo tay song song → rollback toàn bộ (AC 7)
            throw new ApiException(ErrorCodes.Conflict,
                "Dữ liệu quán thay đổi trong lúc import (trùng mã/tên). Hãy chạy lại kiểm tra file.", 409);
        }

        // --- Gắn ảnh SAU transaction (PRD 7.3: ảnh lỗi không làm mất menu — AC 8) ---
        var (imagesAttached, imagesFailed) = await AttachImagesAsync(db, job, productByCode, imageStorage, ct);

        return new CommitOutcome(
            areaByCode.Count, newTables.Count, categoryByCode.Count, productByCode.Count,
            variantsCreated, groupByCode.Count, optionsCreated, linksCreated, menuCreated,
            imagesAttached, imagesFailed, newTables);
    }

    private static async Task<(int Attached, int Failed)> AttachImagesAsync(
        TingGoDbContext db, ImportJob job, Dictionary<string, Product> productByCode,
        IImageStorage imageStorage, CancellationToken ct)
    {
        var assets = await db.Set<ImportAsset>()
            .Where(x => x.ImportJobId == job.Id && x.Status == "staged" && x.TargetEntityCode != null)
            .ToListAsync(ct);
        int attached = 0, failed = 0;
        foreach (var asset in assets)
        {
            try
            {
                if (!productByCode.TryGetValue(asset.TargetEntityCode!, out var product))
                {
                    asset.Status = "unused";
                    continue;
                }
                await using var fileStream = File.OpenRead(asset.StorageKey);
                var url = await imageStorage.SaveAsync(fileStream, asset.ContentType, ct);
                product.ImageUrl = url;
                product.RowVersion++;
                asset.Status = "attached";
                attached++;
            }
            catch (Exception ex)
            {
                asset.Status = "failed";
                asset.ErrorMessage = ex.Message;
                db.Add(new ImportIssue
                {
                    ImportJobId = job.Id, Severity = "WARNING", Code = "IMPORT_IMAGE_ATTACH_FAILED",
                    SheetName = "images/",
                    Message = $"Không gắn được ảnh '{asset.SourceFilename}' — món vẫn được tạo, thêm ảnh sau trên web.",
                });
                failed++;
            }
        }
        if (assets.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        return (attached, failed);
    }
}
