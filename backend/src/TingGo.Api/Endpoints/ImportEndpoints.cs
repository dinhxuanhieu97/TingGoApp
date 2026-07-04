using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TingGo.Api.Import;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Catalog.Domain;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Endpoints;

/// <summary>TingGo Quick Import (docs/prd-quick-import.md) — staging → preview → commit.</summary>
public static class ImportEndpoints
{
    private const string TemplateVersion = "2.0";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // ---------- Template v2 ----------
        endpoints.MapGet("/venues/{venueId:guid}/imports/template", async (
            Guid venueId, ClaimsPrincipal principal,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            await EnsureManagerAsync(venues, memberships, principal, venueId, ct);
            using var workbook = BuildTemplate();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Results.File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "TingGo_Import_Template.xlsx");
        }).RequireAuthorization();

        // ---------- Tạo job: upload + parse + validate → staging ----------
        endpoints.MapPost("/venues/{venueId:guid}/imports", async (
            Guid venueId, IFormFile file, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var (venueInfo, membershipId) = await EnsureManagerAsync(venues, memberships, principal, venueId, ct);
            if (file.Length is 0 or > 10 * 1024 * 1024)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "File phải là .xlsx và nhỏ hơn 10 MB.", 400);
            }

            XLWorkbook workbook;
            try { workbook = new XLWorkbook(file.OpenReadStream()); }
            catch
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Không đọc được file. Hãy dùng file Excel (.xlsx) theo template.", 400);
            }

            using (workbook)
            {
                var job = new ImportJob
                {
                    OrganizationId = venueInfo.OrganizationId,
                    VenueId = venueId,
                    OriginalFilename = file.FileName,
                    TemplateVersion = TemplateVersion,
                    CreatedBy = membershipId,
                };

                var existing = new ExistingVenueData(
                    TableCodes: (await db.Set<DiningTable>().Where(t => t.VenueId == venueId)
                        .Select(t => t.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    ProductSkus: (await db.Set<Product>()
                        .Where(p => p.VenueId == venueId && p.Sku != null)
                        .Select(p => p.Sku!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    CategoryNames: (await (
                        from c in db.Set<MenuCategory>()
                        join m in db.Set<Menu>() on c.MenuId equals m.Id
                        where m.VenueId == venueId
                        select c.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    AreaNames: (await db.Set<VenueArea>().Where(a => a.VenueId == venueId)
                        .Select(a => a.Name).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase));

                var parsed = ImportParser.Parse(workbook, job.Id, existing);

                job.TotalRows = parsed.Rows.Count;
                job.ErrorRows = parsed.Rows.Count(r => r.RowStatus == "error");
                job.WarningRows = parsed.Rows.Count(r => r.RowStatus == "warning");
                job.ValidRows = job.TotalRows - job.ErrorRows;
                job.Status = job.TotalRows == 0
                    ? ImportJobStatus.Failed
                    : job.ErrorRows > 0 ? ImportJobStatus.NeedsReview : ImportJobStatus.ReadyToImport;

                db.Add(job);
                db.AddRange(parsed.Rows);
                db.AddRange(parsed.Issues);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/v1/imports/{job.Id}", await SummaryAsync(db, job, ct));
            }
        }).RequireAuthorization().DisableAntiforgery();

        // ---------- Summary ----------
        endpoints.MapGet("/imports/{importId:guid}", async (
            Guid importId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var job = await LoadJobAsync(db, importId, ct);
            await EnsureManagerAsync(venues, memberships, principal, job.VenueId, ct);
            return Results.Ok(await SummaryAsync(db, job, ct));
        }).RequireAuthorization();

        // ---------- Issues ----------
        endpoints.MapGet("/imports/{importId:guid}/issues", async (
            Guid importId, string? severity, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var job = await LoadJobAsync(db, importId, ct);
            await EnsureManagerAsync(venues, memberships, principal, job.VenueId, ct);
            var query = db.Set<ImportIssue>().AsNoTracking().Where(x => x.ImportJobId == importId);
            if (severity is not null) query = query.Where(x => x.Severity == severity.ToUpperInvariant());
            var issues = await query
                .OrderBy(x => x.Severity).ThenBy(x => x.SheetName).ThenBy(x => x.RowNumber)
                .Select(x => new { x.Severity, x.Code, x.SheetName, x.RowNumber, x.FieldName, x.Message })
                .Take(500)
                .ToListAsync(ct);
            return Results.Ok(issues);
        }).RequireAuthorization();

        // ---------- Commit (AC 6: chặn double-commit bằng atomic status guard) ----------
        endpoints.MapPost("/imports/{importId:guid}/commit", async (
            Guid importId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var job = await LoadJobAsync(db, importId, ct);
            var (venueInfo, membershipId) = await EnsureManagerAsync(venues, memberships, principal, job.VenueId, ct);

            if (job.Status == ImportJobStatus.NeedsReview)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    $"File còn {job.ErrorRows} dòng lỗi — sửa file và tải lại trước khi import.", 409);
            }

            var claimed = await db.Set<ImportJob>()
                .Where(x => x.Id == importId && x.Status == ImportJobStatus.ReadyToImport)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportJobStatus.Importing), ct);
            if (claimed == 0)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    "Job không ở trạng thái sẵn sàng (đã import, đã hủy hoặc đang chạy).", 409);
            }

            try
            {
                var outcome = await ImportCommitter.CommitAsync(db, job, membershipId, venueInfo.CurrencyCode, ct);
                job.Status = job.WarningRows > 0
                    ? ImportJobStatus.CompletedWithWarnings
                    : ImportJobStatus.Completed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                db.Update(job);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { job.Id, job.Status, outcome });
            }
            catch
            {
                await db.Set<ImportJob>().Where(x => x.Id == importId)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportJobStatus.ReadyToImport), ct);
                throw;
            }
        }).RequireAuthorization();

        // ---------- Cancel (AC 13) ----------
        endpoints.MapPost("/imports/{importId:guid}/cancel", async (
            Guid importId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var job = await LoadJobAsync(db, importId, ct);
            await EnsureManagerAsync(venues, memberships, principal, job.VenueId, ct);
            var cancelled = await db.Set<ImportJob>()
                .Where(x => x.Id == importId &&
                            (x.Status == ImportJobStatus.ReadyToImport || x.Status == ImportJobStatus.NeedsReview))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ImportJobStatus.Cancelled), ct);
            if (cancelled == 0)
            {
                throw new ApiException(ErrorCodes.Conflict, "Job không thể hủy ở trạng thái hiện tại.", 409);
            }
            return Results.Ok(new { importId, status = ImportJobStatus.Cancelled });
        }).RequireAuthorization();

        // ---------- Error file (AC 14) ----------
        endpoints.MapGet("/imports/{importId:guid}/error-file", async (
            Guid importId, ClaimsPrincipal principal, TingGoDbContext db,
            IVenueDirectory venues, IMembershipService memberships, CancellationToken ct) =>
        {
            var job = await LoadJobAsync(db, importId, ct);
            await EnsureManagerAsync(venues, memberships, principal, job.VenueId, ct);
            var issues = await db.Set<ImportIssue>().AsNoTracking()
                .Where(x => x.ImportJobId == importId && x.Severity != "INFO")
                .OrderBy(x => x.SheetName).ThenBy(x => x.RowNumber)
                .ToListAsync(ct);

            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Loi");
            string[] headers = ["Sheet", "Dòng", "Cột", "Mức độ", "Mã lỗi", "Nội dung"];
            for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Row(1).Style.Font.Bold = true;
            var rowIndex = 2;
            foreach (var issue in issues)
            {
                sheet.Cell(rowIndex, 1).Value = issue.SheetName;
                sheet.Cell(rowIndex, 2).Value = issue.RowNumber;
                sheet.Cell(rowIndex, 3).Value = issue.FieldName;
                sheet.Cell(rowIndex, 4).Value = issue.Severity;
                sheet.Cell(rowIndex, 5).Value = issue.Code;
                sheet.Cell(rowIndex, 6).Value = issue.Message;
                if (issue.Severity == "ERROR")
                {
                    sheet.Row(rowIndex).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 235, 235);
                }
                rowIndex++;
            }
            sheet.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return Results.File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "TingGo_Import_Errors.xlsx");
        }).RequireAuthorization();
    }

    private static async Task<object> SummaryAsync(TingGoDbContext db, ImportJob job, CancellationToken ct)
    {
        var sections = await db.Set<ImportRow>().AsNoTracking()
            .Where(x => x.ImportJobId == job.Id)
            .GroupBy(x => x.SectionType)
            .Select(g => new
            {
                section = g.Key,
                total = g.Count(),
                errors = g.Count(x => x.RowStatus == "error"),
                warnings = g.Count(x => x.RowStatus == "warning"),
            })
            .ToListAsync(ct);
        var issueCounts = await db.Set<ImportIssue>().AsNoTracking()
            .Where(x => x.ImportJobId == job.Id)
            .GroupBy(x => x.Severity)
            .Select(g => new { severity = g.Key, count = g.Count() })
            .ToListAsync(ct);
        return new
        {
            importId = job.Id,
            job.Status,
            job.OriginalFilename,
            job.TotalRows, job.ValidRows, job.WarningRows, job.ErrorRows,
            sections,
            issues = issueCounts,
            canCommit = job.Status == ImportJobStatus.ReadyToImport,
        };
    }

    private static async Task<ImportJob> LoadJobAsync(TingGoDbContext db, Guid importId, CancellationToken ct)
        => await db.Set<ImportJob>().FirstOrDefaultAsync(x => x.Id == importId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy import job.", 404);

    private static async Task<(VenueInfo Venue, Guid MembershipId)> EnsureManagerAsync(
        IVenueDirectory venues, IMembershipService memberships, ClaimsPrincipal principal,
        Guid venueId, CancellationToken ct)
    {
        var venueInfo = await venues.GetVenueInfoAsync(venueId, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy quán.", 404);
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        var membership = await memberships.GetMembershipAsync(Guid.Parse(sub), venueInfo.OrganizationId, ct)
            ?? throw new ApiException(ErrorCodes.Forbidden, "Bạn không thuộc quán này.", 403);
        if (membership.Role is not ("owner" or "manager"))
        {
            throw new ApiException(ErrorCodes.Forbidden, "Chỉ owner/manager được import dữ liệu.", 403);
        }
        return (venueInfo, membership.MembershipId);
    }

    private static XLWorkbook BuildTemplate()
    {
        var workbook = new XLWorkbook();

        var readme = workbook.Worksheets.Add("README");
        readme.Cell(1, 1).Value = "TingGo Quick Import — template_version = 2.0";
        readme.Cell(3, 1).Value = "Cách dùng: điền các sheet bên cạnh rồi tải file lên TingGo (Menu → Nhập dữ liệu nhanh).";
        readme.Cell(4, 1).Value = "Sheet nâng cao (Variants, ModifierGroups, ModifierOptions, ProductModifiers) có thể để trống.";
        readme.Cell(5, 1).Value = "Các cột *_code: chỉ chữ/số/gạch dưới, KHÔNG trùng nhau, dùng để liên kết giữa các sheet.";
        readme.Cell(6, 1).Value = "Giá: nhập số nguyên đồng (25000), KHÔNG ghi kèm 'VND' hay '₫'.";
        readme.Cell(7, 1).Value = "TRUE/FALSE cho các cột is_*; để trống = giá trị mặc định.";
        readme.Cell(8, 1).Value = "Sau khi import: menu ở trạng thái NHÁP — kiểm tra rồi bấm Công bố; QR tự tạo cho bàn active.";
        readme.Cell(10, 1).Value = "Lỗi thường gặp: trùng code; category_code/area_code không tồn tại; giá ghi kèm chữ.";
        readme.Column(1).Width = 110;

        AddSheet(workbook, "Venue",
            ["venue_name (đối chiếu)", "default_locale", "currency_code", "timezone", "wifi_name", "phone", "email", "address", "tax_rate"],
            [["TingGo Cafe", "vi-VN", "VND", "Asia/Ho_Chi_Minh", "TingGo Guest", "", "", "", ""]]);
        AddSheet(workbook, "Areas",
            ["area_code", "area_name", "sort_order", "is_active"],
            [["FLOOR_1", "Tầng 1", "1", "TRUE"], ["GARDEN", "Sân vườn", "2", "TRUE"]]);
        AddSheet(workbook, "Tables",
            ["table_code", "table_name", "area_code", "capacity", "sort_order", "is_active"],
            [["T01", "Bàn 1", "FLOOR_1", "4", "1", "TRUE"], ["T02", "Bàn 2", "FLOOR_1", "2", "2", "TRUE"]]);
        AddSheet(workbook, "Categories",
            ["category_code", "category_name", "description", "sort_order", "is_visible"],
            [["COFFEE", "Cà phê", "Các loại cà phê", "1", "TRUE"], ["TEA", "Trà", "", "2", "TRUE"]]);
        AddSheet(workbook, "Products",
            ["product_code", "category_code", "product_name", "description", "base_price", "image_file", "is_available", "sort_order"],
            [["CF_SUA", "COFFEE", "Cà phê sữa", "Cà phê pha phin với sữa", "25000", "", "TRUE", "1"],
             ["TRA_DAO", "TEA", "Trà đào cam sả", "", "39000", "", "TRUE", "2"]]);
        AddSheet(workbook, "Variants",
            ["variant_code", "product_code", "variant_name", "price_delta", "is_default", "is_available", "sort_order"],
            [["CF_SUA_M", "CF_SUA", "Size M", "0", "TRUE", "TRUE", "1"],
             ["CF_SUA_L", "CF_SUA", "Size L", "10000", "FALSE", "TRUE", "2"]]);
        AddSheet(workbook, "ModifierGroups",
            ["group_code", "group_name", "min_select", "max_select", "is_required", "sort_order"],
            [["SUGAR", "Mức đường", "1", "1", "TRUE", "1"], ["TOPPING", "Topping", "0", "3", "FALSE", "2"]]);
        AddSheet(workbook, "ModifierOptions",
            ["option_code", "group_code", "option_name", "price_delta", "is_available", "sort_order"],
            [["SUGAR_100", "SUGAR", "100% đường", "0", "TRUE", "1"],
             ["SUGAR_50", "SUGAR", "50% đường", "0", "TRUE", "2"],
             ["TP_TRANCHAU", "TOPPING", "Trân châu", "7000", "TRUE", "1"]]);
        AddSheet(workbook, "ProductModifiers",
            ["product_code", "group_code", "sort_order"],
            [["TRA_DAO", "SUGAR", "1"], ["TRA_DAO", "TOPPING", "2"]]);

        return workbook;
    }

    private static void AddSheet(XLWorkbook workbook, string name, string[] headers, string[][] examples)
    {
        var sheet = workbook.Worksheets.Add(name);
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 237, 213);
        for (var r = 0; r < examples.Length; r++)
        {
            for (var c = 0; c < examples[r].Length; c++)
            {
                sheet.Cell(r + 2, c + 1).Value = examples[r][c];
            }
        }
        sheet.Columns().AdjustToContents();
    }
}
