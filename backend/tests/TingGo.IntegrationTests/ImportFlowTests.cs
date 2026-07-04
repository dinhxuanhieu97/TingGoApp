using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Quick Import v2 — theo Acceptance Criteria PRD mục 16 (trừ ZIP/ảnh).</summary>
public class ImportFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    private async Task<(HttpClient Owner, Guid VenueId, string? Slug)> SetupVenueAsync()
    {
        var owner = factory.CreateClient();
        var email = $"qi-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "QI" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán QI" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return (owner, venue.GetProperty("id").GetGuid(), venue.GetProperty("slug").GetString());
    }

    private static MultipartFormDataContent Multipart(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(stream.ToArray());
        fileContent.Headers.ContentType =
            new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "import.xlsx");
        return content;
    }

    private static XLWorkbook GoodWorkbook()
    {
        var workbook = new XLWorkbook();
        var areas = workbook.Worksheets.Add("Areas");
        areas.Cell(1, 1).Value = "area_code";
        areas.Cell(2, 1).Value = "FLOOR_1"; areas.Cell(2, 2).Value = "Tầng 1";
        var tables = workbook.Worksheets.Add("Tables");
        tables.Cell(1, 1).Value = "table_code";
        tables.Cell(2, 1).Value = "T01"; tables.Cell(2, 2).Value = "Bàn 1"; tables.Cell(2, 3).Value = "FLOOR_1";
        tables.Cell(3, 1).Value = "T02"; tables.Cell(3, 2).Value = "Bàn 2"; tables.Cell(3, 3).Value = "FLOOR_1";
        var categories = workbook.Worksheets.Add("Categories");
        categories.Cell(1, 1).Value = "category_code";
        categories.Cell(2, 1).Value = "COFFEE"; categories.Cell(2, 2).Value = "Cà phê";
        var products = workbook.Worksheets.Add("Products");
        products.Cell(1, 1).Value = "product_code";
        products.Cell(2, 1).Value = "CF_SUA"; products.Cell(2, 2).Value = "COFFEE";
        products.Cell(2, 3).Value = "Cà phê sữa"; products.Cell(2, 4).Value = "Phin + sữa đặc";
        products.Cell(2, 5).Value = 25000;
        var variants = workbook.Worksheets.Add("Variants");
        variants.Cell(1, 1).Value = "variant_code";
        variants.Cell(2, 1).Value = "CF_SUA_L"; variants.Cell(2, 2).Value = "CF_SUA";
        variants.Cell(2, 3).Value = "Size L"; variants.Cell(2, 4).Value = 10000;
        var groups = workbook.Worksheets.Add("ModifierGroups");
        groups.Cell(1, 1).Value = "group_code";
        groups.Cell(2, 1).Value = "SUGAR"; groups.Cell(2, 2).Value = "Mức đường";
        groups.Cell(2, 3).Value = 1; groups.Cell(2, 4).Value = 1; groups.Cell(2, 5).Value = "TRUE";
        var options = workbook.Worksheets.Add("ModifierOptions");
        options.Cell(1, 1).Value = "option_code";
        options.Cell(2, 1).Value = "SUGAR_50"; options.Cell(2, 2).Value = "SUGAR";
        options.Cell(2, 3).Value = "50% đường";
        var links = workbook.Worksheets.Add("ProductModifiers");
        links.Cell(1, 1).Value = "product_code";
        links.Cell(2, 1).Value = "CF_SUA"; links.Cell(2, 2).Value = "SUGAR";
        return workbook;
    }

    [Fact] // AC 1,2,3,6,9,10,13,14
    public async Task QuickImport_FullFlow()
    {
        var (owner, venueId, slug) = await SetupVenueAsync();

        // AC1: template
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/api/v1/venues/{venueId}/imports/template")).StatusCode);

        // Upload file hợp lệ → READY_TO_IMPORT
        using var workbook = GoodWorkbook();
        var created = await owner.PostAsync($"/api/v1/venues/{venueId}/imports", Multipart(workbook));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("READY_TO_IMPORT", summary.GetProperty("status").GetString());
        Assert.True(summary.GetProperty("canCommit").GetBoolean());
        var importId = summary.GetProperty("importId").GetGuid();

        // DB chính chưa có gì trước commit (staging only)
        var beforeCommit = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/products");
        Assert.Equal(0, beforeCommit.GetArrayLength());

        // Commit → tạo đủ (AC2,3,9)
        var commit = await owner.PostAsync($"/api/v1/imports/{importId}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var outcome = (await commit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome");
        Assert.Equal(1, outcome.GetProperty("areasCreated").GetInt32());
        Assert.Equal(2, outcome.GetProperty("tablesCreated").GetInt32());
        Assert.Equal(1, outcome.GetProperty("productsCreated").GetInt32());
        Assert.Equal(1, outcome.GetProperty("variantsCreated").GetInt32());
        Assert.True(outcome.GetProperty("menuCreated").GetBoolean());
        var rawToken = outcome.GetProperty("newTables")[0].GetProperty("rawToken").GetString();

        // AC9: QR bàn hoạt động
        var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/v1/public/q/{rawToken}")).StatusCode);

        // AC10: menu ở trạng thái NHÁP → public menu 404 MENU_NOT_PUBLISHED
        var publicMenu = await guest.GetAsync($"/api/v1/public/venues/{slug}/menu");
        Assert.Equal(HttpStatusCode.NotFound, publicMenu.StatusCode);

        // AC6: commit lần 2 → 409, không nhân đôi
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsync($"/api/v1/imports/{importId}/commit", null)).StatusCode);
        var products = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/products");
        Assert.Equal(1, products.GetArrayLength());

        // AC13: job đã hoàn thành không hủy được
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsync($"/api/v1/imports/{importId}/cancel", null)).StatusCode);
    }

    [Fact] // AC 4,5,14: file lỗi → không ghi DB, issues đúng dòng, tải file lỗi
    public async Task QuickImport_FileLoi_KhongGhiDb()
    {
        var (owner, venueId, _) = await SetupVenueAsync();

        using var workbook = new XLWorkbook();
        var categories = workbook.Worksheets.Add("Categories");
        categories.Cell(1, 1).Value = "category_code";
        categories.Cell(2, 1).Value = "COFFEE"; categories.Cell(2, 2).Value = "Cà phê";
        var products = workbook.Worksheets.Add("Products");
        products.Cell(1, 1).Value = "product_code";
        // Dòng 2: category không tồn tại; dòng 3: giá kèm chữ; dòng 4: trùng code với dòng 2
        products.Cell(2, 1).Value = "P1"; products.Cell(2, 2).Value = "NO_CAT";
        products.Cell(2, 3).Value = "Món 1"; products.Cell(2, 5).Value = 10000;
        products.Cell(3, 1).Value = "P2"; products.Cell(3, 2).Value = "COFFEE";
        products.Cell(3, 3).Value = "Món 2"; products.Cell(3, 5).Value = "25000 VND";
        products.Cell(4, 1).Value = "p1"; products.Cell(4, 2).Value = "COFFEE";
        products.Cell(4, 3).Value = "Món 3"; products.Cell(4, 5).Value = 5000;

        var created = await owner.PostAsync($"/api/v1/venues/{venueId}/imports", Multipart(workbook));
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("NEEDS_REVIEW", summary.GetProperty("status").GetString());
        Assert.Equal(3, summary.GetProperty("errorRows").GetInt32());
        var importId = summary.GetProperty("importId").GetGuid();

        // AC5: issues đúng sheet + dòng
        var issues = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{importId}/issues?severity=ERROR");
        var list = issues.EnumerateArray().ToList();
        Assert.Contains(list, i => i.GetProperty("rowNumber").GetInt32() == 2
            && i.GetProperty("code").GetString() == "IMPORT_LINK_NOT_FOUND");
        Assert.Contains(list, i => i.GetProperty("rowNumber").GetInt32() == 3
            && i.GetProperty("code").GetString() == "IMPORT_INVALID_PRICE");
        Assert.Contains(list, i => i.GetProperty("rowNumber").GetInt32() == 4
            && i.GetProperty("code").GetString() == "IMPORT_DUPLICATE_CODE");

        // Commit bị chặn (AC4) — DB chính không có món nào
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsync($"/api/v1/imports/{importId}/commit", null)).StatusCode);
        var dbProducts = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/products");
        Assert.Equal(0, dbProducts.GetArrayLength());

        // AC14: file báo lỗi
        Assert.Equal(HttpStatusCode.OK,
            (await owner.GetAsync($"/api/v1/imports/{importId}/error-file")).StatusCode);

        // AC13: hủy được job đang NEEDS_REVIEW
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync($"/api/v1/imports/{importId}/cancel", null)).StatusCode);
    }

    [Fact] // AC 12: tenant isolation
    public async Task QuickImport_KhongCoQuyen_403()
    {
        var (ownerA, venueA, _) = await SetupVenueAsync();
        var (ownerB, _, _) = await SetupVenueAsync();

        using var workbook = GoodWorkbook();
        var upload = await ownerB.PostAsync($"/api/v1/venues/{venueA}/imports", Multipart(workbook));
        Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);

        // Và commit chéo job cũng bị chặn
        var created = await ownerA.PostAsync($"/api/v1/venues/{venueA}/imports", Multipart(GoodWorkbook()));
        var importId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("importId").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ownerB.PostAsync($"/api/v1/imports/{importId}/commit", null)).StatusCode);
    }
}
