using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using TingGo.Infrastructure.Persistence;

namespace TingGo.Api.Import;

/// <summary>
/// Đọc + chuẩn hóa + validate workbook → staging rows/issues. KHÔNG ghi DB chính.
/// Validation theo PRD mục 11.2: code, giá, liên kết, min/max modifier.
/// </summary>
public sealed class ImportParseResult
{
    public List<ImportRow> Rows { get; } = [];
    public List<ImportIssue> Issues { get; } = [];
}

public sealed record ExistingVenueData(
    HashSet<string> TableCodes,
    HashSet<string> ProductSkus,
    HashSet<string> CategoryNames,
    HashSet<string> AreaNames);

public static partial class ImportParser
{
    private const long MaxPriceMinor = 100_000_000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex CodeRegex();

    public static ImportParseResult Parse(
        XLWorkbook workbook, Guid jobId, ExistingVenueData existing,
        IReadOnlySet<string>? availableImages = null)
    {
        availableImages ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new ImportParseResult();
        var codes = new Dictionary<string, HashSet<string>>(); // section → codes (upper)

        HashSet<string> Codes(string section) =>
            codes.TryGetValue(section, out var set) ? set : codes[section] = new(StringComparer.OrdinalIgnoreCase);

        // --- Venue (1 dòng) ---
        ParseSheet(workbook, "Venue", result, jobId, ImportSections.Venue, (row, ctx) =>
        {
            var locale = Str(row, 2);
            var currency = Str(row, 3);
            var timezone = Str(row, 4);
            var wifi = Str(row, 5);
            foreach (var (value, field) in new[] { (Str(row, 6), "phone"), (Str(row, 7), "email"), (Str(row, 8), "address"), (Str(row, 9), "tax_rate") })
            {
                if (value.Length > 0)
                {
                    ctx.Info("IMPORT_FIELD_NOT_SUPPORTED", field, $"Cột {field} chưa được hỗ trợ — bỏ qua.");
                }
            }
            if (currency.Length > 0 && currency.Length != 3)
            {
                ctx.Error("IMPORT_INVALID_CURRENCY", "currency_code", "currency_code phải là mã 3 ký tự (VD: VND).");
            }
            if (timezone.Length > 0)
            {
                try { TimeZoneInfo.FindSystemTimeZoneById(timezone); }
                catch { ctx.Error("IMPORT_INVALID_TIMEZONE", "timezone", $"Timezone '{timezone}' không hợp lệ."); }
            }
            return (new VenueRowData(NullIf(wifi), NullIf(locale), NullIf(currency), NullIf(timezone)), null);
        });

        // --- Areas ---
        ParseSheet(workbook, "Areas", result, jobId, ImportSections.Areas, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "area_code", ctx, Codes(ImportSections.Areas));
            var name = RequireText(row, 2, "area_name", 100, ctx);
            if (code is null || name is null) return (null, code);
            if (existing.AreaNames.Contains(name))
            {
                ctx.Error("IMPORT_DUPLICATE_AREA", "area_name", $"Khu vực '{name}' đã tồn tại trong quán (chế độ Create Only).");
                return (null, code);
            }
            return (new AreaRowData(code, name, Int(row, 3, 0), Bool(row, 4, true)), code);
        });

        // --- Tables ---
        ParseSheet(workbook, "Tables", result, jobId, ImportSections.Tables, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "table_code", ctx, Codes(ImportSections.Tables));
            var name = Str(row, 2);
            var areaCode = Str(row, 3).ToUpperInvariant();
            if (code is null) return (null, null);
            if (areaCode.Length == 0)
            {
                ctx.Error("IMPORT_MISSING_LINK", "area_code", "Bàn phải có area_code.");
                return (null, code);
            }
            if (!Codes(ImportSections.Areas).Contains(areaCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "area_code", $"area_code '{areaCode}' không có trong sheet Areas.");
                return (null, code);
            }
            if (existing.TableCodes.Contains(code))
            {
                ctx.Error("IMPORT_DUPLICATE_TABLE_CODE", "table_code", $"Mã bàn '{code}' đã tồn tại trong quán.");
                return (null, code);
            }
            if (Str(row, 4).Length > 0)
            {
                ctx.Info("IMPORT_FIELD_NOT_SUPPORTED", "capacity", "capacity chưa được hỗ trợ — bỏ qua.");
            }
            return (new TableRowData(code, name.Length > 0 ? name : code, areaCode, Int(row, 5, 0), Bool(row, 6, true)), code);
        });

        // --- Categories ---
        ParseSheet(workbook, "Categories", result, jobId, ImportSections.Categories, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "category_code", ctx, Codes(ImportSections.Categories));
            var name = RequireText(row, 2, "category_name", 200, ctx);
            if (code is null || name is null) return (null, code);
            if (existing.CategoryNames.Contains(name))
            {
                ctx.Error("IMPORT_DUPLICATE_CATEGORY", "category_name", $"Danh mục '{name}' đã tồn tại trong quán.");
                return (null, code);
            }
            return (new CategoryRowData(code, name, NullIf(Str(row, 3)), Int(row, 4, 0), Bool(row, 5, true)), code);
        });

        // --- Products ---
        ParseSheet(workbook, "Products", result, jobId, ImportSections.Products, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "product_code", ctx, Codes(ImportSections.Products));
            var categoryCode = Str(row, 2).ToUpperInvariant();
            var name = RequireText(row, 3, "product_name", 200, ctx);
            if (code is null || name is null) return (null, code);
            if (!Codes(ImportSections.Categories).Contains(categoryCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "category_code", $"category_code '{categoryCode}' không có trong sheet Categories.");
                return (null, code);
            }
            if (existing.ProductSkus.Contains(code))
            {
                ctx.Error("IMPORT_DUPLICATE_PRODUCT_CODE", "product_code", $"product_code '{code}' đã tồn tại trong quán.");
                return (null, code);
            }
            var price = Price(row, 5, "base_price", ctx);
            if (price is null) return (null, code);

            var imageFile = Str(row, 6);
            if (imageFile.Length > 0 && !imageFile.StartsWith("http"))
            {
                // Chấp nhận "images/x.jpg" hoặc "x.jpg" — so theo tên file trong gói ZIP
                var imageName = Path.GetFileName(imageFile);
                if (availableImages.Contains(imageName))
                {
                    imageFile = imageName;
                }
                else
                {
                    ctx.Warning("IMPORT_IMAGE_NOT_FOUND", "image_file",
                        $"Không tìm thấy ảnh '{imageFile}' trong gói ZIP — món được tạo không ảnh.");
                    imageFile = "";
                }
            }
            var description = Str(row, 4);
            if (description.Length == 0)
            {
                ctx.Warning("IMPORT_MISSING_DESCRIPTION", "description", "Món chưa có mô tả.");
            }
            return (new ProductRowData(code, categoryCode, name, NullIf(description),
                price.Value, NullIf(imageFile), Bool(row, 7, true), Int(row, 8, 0)), code);
        });

        // --- Variants ---
        ParseSheet(workbook, "Variants", result, jobId, ImportSections.Variants, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "variant_code", ctx, Codes(ImportSections.Variants));
            var productCode = Str(row, 2).ToUpperInvariant();
            var name = RequireText(row, 3, "variant_name", 100, ctx);
            if (code is null || name is null) return (null, code);
            if (!Codes(ImportSections.Products).Contains(productCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "product_code", $"product_code '{productCode}' không có trong sheet Products.");
                return (null, code);
            }
            var delta = Price(row, 4, "price_delta", ctx, allowNegative: true);
            if (delta is null) return (null, code);
            return (new VariantRowData(code, productCode, name, delta.Value,
                Bool(row, 5, false), Bool(row, 6, true), Int(row, 7, 0)), code);
        });

        // --- ModifierGroups ---
        ParseSheet(workbook, "ModifierGroups", result, jobId, ImportSections.ModifierGroups, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "group_code", ctx, Codes(ImportSections.ModifierGroups));
            var name = RequireText(row, 2, "group_name", 200, ctx);
            if (code is null || name is null) return (null, code);
            var min = Int(row, 3, 0);
            var max = Int(row, 4, 1);
            if (min < 0 || max < 1 || min > max)
            {
                ctx.Error("IMPORT_INVALID_SELECT_RANGE", "min_select", "Cần 0 ≤ min_select ≤ max_select và max_select ≥ 1.");
                return (null, code);
            }
            return (new GroupRowData(code, name, min, max, Bool(row, 5, false), Int(row, 6, 0)), code);
        });

        // --- ModifierOptions ---
        ParseSheet(workbook, "ModifierOptions", result, jobId, ImportSections.ModifierOptions, (row, ctx) =>
        {
            var code = RequireCode(row, 1, "option_code", ctx, Codes(ImportSections.ModifierOptions));
            var groupCode = Str(row, 2).ToUpperInvariant();
            var name = RequireText(row, 3, "option_name", 200, ctx);
            if (code is null || name is null) return (null, code);
            if (!Codes(ImportSections.ModifierGroups).Contains(groupCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "group_code", $"group_code '{groupCode}' không có trong sheet ModifierGroups.");
                return (null, code);
            }
            var delta = Price(row, 4, "price_delta", ctx, optional: true);
            return (new OptionRowData(code, groupCode, name, delta ?? 0, Bool(row, 5, true), Int(row, 6, 0)), code);
        });

        // --- ProductModifiers ---
        ParseSheet(workbook, "ProductModifiers", result, jobId, ImportSections.ProductModifiers, (row, ctx) =>
        {
            var productCode = Str(row, 1).ToUpperInvariant();
            var groupCode = Str(row, 2).ToUpperInvariant();
            if (productCode.Length == 0 || groupCode.Length == 0)
            {
                ctx.Error("IMPORT_MISSING_LINK", "product_code", "Cần cả product_code và group_code.");
                return (null, null);
            }
            if (!Codes(ImportSections.Products).Contains(productCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "product_code", $"product_code '{productCode}' không có trong sheet Products.");
                return (null, null);
            }
            if (!Codes(ImportSections.ModifierGroups).Contains(groupCode))
            {
                ctx.Error("IMPORT_LINK_NOT_FOUND", "group_code", $"group_code '{groupCode}' không có trong sheet ModifierGroups.");
                return (null, null);
            }
            return (new ProductModifierRowData(productCode, groupCode, Int(row, 3, 0)), $"{productCode}|{groupCode}");
        });

        // --- Cross-sheet: nhóm bắt buộc phải có option; min_select ≤ số option ---
        ValidateGroupOptionCounts(result, jobId);

        // Sheet chưa hỗ trợ
        foreach (var sheetName in new[] { "OpeningHours", "Translations" })
        {
            if (workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet) && sheet.RowsUsed().Skip(1).Any())
            {
                result.Issues.Add(new ImportIssue
                {
                    ImportJobId = jobId, Severity = "INFO", Code = "IMPORT_SHEET_NOT_SUPPORTED",
                    SheetName = sheetName,
                    Message = $"Sheet {sheetName} chưa được hỗ trợ ở phiên bản này — dữ liệu bị bỏ qua.",
                });
            }
        }

        return result;
    }

    private static void ValidateGroupOptionCounts(ImportParseResult result, Guid jobId)
    {
        var optionCounts = result.Rows
            .Where(r => r.SectionType == ImportSections.ModifierOptions && r.RowStatus != "error")
            .Select(r => JsonSerializer.Deserialize<OptionRowData>(r.NormalizedData, Json)!)
            .GroupBy(o => o.GroupCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in result.Rows.Where(r => r.SectionType == ImportSections.ModifierGroups && r.RowStatus != "error"))
        {
            var group = JsonSerializer.Deserialize<GroupRowData>(row.NormalizedData, Json)!;
            var count = optionCounts.GetValueOrDefault(group.Code, 0);
            if ((group.IsRequired && count == 0) || group.MinSelect > count)
            {
                row.RowStatus = "error";
                result.Issues.Add(new ImportIssue
                {
                    ImportJobId = jobId, ImportRowId = row.Id, Severity = "ERROR",
                    Code = "IMPORT_GROUP_WITHOUT_OPTIONS", SheetName = row.SheetName, RowNumber = row.RowNumber,
                    FieldName = "min_select",
                    Message = $"Nhóm '{group.Code}' cần ít nhất {Math.Max(group.MinSelect, 1)} lựa chọn trong sheet ModifierOptions (hiện có {count}).",
                });
            }
        }
    }

    // ---------- Sheet iteration ----------
    private sealed class RowContext(ImportParseResult result, Guid jobId, ImportRow row)
    {
        public bool HasError { get; private set; }
        public bool HasWarning { get; private set; }

        public void Error(string code, string field, string message)
        {
            HasError = true;
            Add("ERROR", code, field, message);
        }

        public void Warning(string code, string field, string message)
        {
            HasWarning = true;
            Add("WARNING", code, field, message);
        }

        public void Info(string code, string field, string message) => Add("INFO", code, field, message);

        private void Add(string severity, string code, string field, string message)
            => result.Issues.Add(new ImportIssue
            {
                ImportJobId = jobId, ImportRowId = row.Id, Severity = severity, Code = code,
                SheetName = row.SheetName, RowNumber = row.RowNumber, FieldName = field, Message = message,
            });
    }

    private static void ParseSheet(
        XLWorkbook workbook, string sheetName, ImportParseResult result, Guid jobId, string section,
        Func<IXLRow, RowContext, (object? Data, string? EntityCode)> parseRow)
    {
        if (!workbook.Worksheets.TryGetWorksheet(sheetName, out var sheet)) return;
        foreach (var xlRow in sheet.RowsUsed().Skip(1))
        {
            if (xlRow.CellsUsed().All(c => c.GetString().Trim().Length == 0)) continue;
            var row = new ImportRow
            {
                ImportJobId = jobId, SectionType = section, SheetName = sheetName,
                RowNumber = xlRow.RowNumber(),
                SourceData = JsonSerializer.Serialize(
                    xlRow.Cells(1, 9).Select(c => c.GetString().Trim()).ToArray(), Json),
            };
            var ctx = new RowContext(result, jobId, row);
            var (data, entityCode) = parseRow(xlRow, ctx);
            row.EntityCode = entityCode;
            row.RowStatus = ctx.HasError ? "error" : ctx.HasWarning ? "warning" : "valid";
            if (data is not null)
            {
                row.NormalizedData = JsonSerializer.Serialize(data, data.GetType(), Json);
            }
            result.Rows.Add(row);
        }
    }

    // ---------- Cell helpers ----------
    private static string Str(IXLRow row, int column) => row.Cell(column).GetString().Trim();

    private static string? NullIf(string value) => value.Length == 0 ? null : value;

    private static int Int(IXLRow row, int column, int fallback)
        => row.Cell(column).TryGetValue<int>(out var value) ? value : fallback;

    private static bool Bool(IXLRow row, int column, bool fallback)
    {
        var raw = Str(row, column).ToUpperInvariant();
        return raw switch
        {
            "TRUE" or "1" or "YES" or "CO" or "CÓ" => true,
            "FALSE" or "0" or "NO" or "KHONG" or "KHÔNG" => false,
            _ => fallback,
        };
    }

    private static string? RequireCode(IXLRow row, int column, string field, RowContext ctx, HashSet<string> seen)
    {
        var code = Str(row, column).ToUpperInvariant();
        if (code.Length == 0)
        {
            ctx.Error("IMPORT_MISSING_CODE", field, $"{field} không được để trống.");
            return null;
        }
        if (!CodeRegex().IsMatch(code))
        {
            ctx.Error("IMPORT_INVALID_CODE", field, $"{field} chỉ được chứa chữ, số, gạch dưới/ngang, tối đa 64 ký tự.");
            return null;
        }
        if (!seen.Add(code))
        {
            ctx.Error("IMPORT_DUPLICATE_CODE", field, $"{field} '{code}' bị trùng trong file (không phân biệt hoa thường).");
            return null;
        }
        return code;
    }

    private static string? RequireText(IXLRow row, int column, string field, int maxLength, RowContext ctx)
    {
        var value = Str(row, column);
        if (value.Length == 0)
        {
            ctx.Error("IMPORT_MISSING_VALUE", field, $"{field} không được để trống.");
            return null;
        }
        if (value.Length > maxLength)
        {
            ctx.Error("IMPORT_VALUE_TOO_LONG", field, $"{field} tối đa {maxLength} ký tự.");
            return null;
        }
        return value;
    }

    private static long? Price(IXLRow row, int column, string field, RowContext ctx,
        bool allowNegative = false, bool optional = false)
    {
        var raw = Str(row, column);
        if (raw.Length == 0)
        {
            if (optional) return 0;
            ctx.Error("IMPORT_INVALID_PRICE", field, $"{field} không được để trống.");
            return null;
        }
        // Không chấp nhận ký hiệu tiền trong ô (PRD 11.2); cho phép dấu phân tách nghìn
        var cleaned = raw.Replace(".", "").Replace(",", "").Replace(" ", "");
        if (!long.TryParse(cleaned, out var value))
        {
            ctx.Error("IMPORT_INVALID_PRICE", field, $"{field} '{raw}' không phải số hợp lệ (không ghi kèm VND/₫).");
            return null;
        }
        if (!allowNegative && value < 0)
        {
            ctx.Error("IMPORT_NEGATIVE_PRICE", field, $"{field} không được âm.");
            return null;
        }
        if (Math.Abs(value) > MaxPriceMinor)
        {
            ctx.Error("IMPORT_PRICE_TOO_LARGE", field, $"{field} vượt giới hạn {MaxPriceMinor:N0}.");
            return null;
        }
        return value;
    }
}
