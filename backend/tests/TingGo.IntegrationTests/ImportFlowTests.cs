using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Import Excel: template + nhập món/size/topping/khu vực/bàn + skip trùng.</summary>
public class ImportFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task Import_TaoDuLieuDayDu_VaSkipTrung()
    {
        // Setup: owner + org + venue trống
        var owner = factory.CreateClient();
        var email = $"imp-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Imp" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán Import" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();
        var slug = venue.GetProperty("slug").GetString();

        // 1. Template tải được
        var template = await owner.GetAsync($"/api/v1/venues/{venueId}/import/template");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);

        // 2. Tạo file import: 2 món (1 có size, 1 có topping, 1 dòng lỗi giá) + 2 bàn
        using var workbook = new XLWorkbook();
        var menuSheet = workbook.Worksheets.Add("Mon");
        menuSheet.Cell(1, 1).Value = "Danh mục";
        menuSheet.Cell(2, 1).Value = "Cà phê";
        menuSheet.Cell(2, 2).Value = "Bạc xỉu";
        menuSheet.Cell(2, 3).Value = 32000;
        menuSheet.Cell(2, 4).Value = "Cà phê sữa kiểu Sài Gòn";
        menuSheet.Cell(2, 6).Value = "M:0; L:5000";
        menuSheet.Cell(3, 1).Value = "Trà";
        menuSheet.Cell(3, 2).Value = "Trà đào";
        menuSheet.Cell(3, 3).Value = 39000;
        menuSheet.Cell(3, 7).Value = "Trân châu:7000; Thạch:5000";
        menuSheet.Cell(4, 1).Value = "Trà";
        menuSheet.Cell(4, 2).Value = "Món lỗi";
        menuSheet.Cell(4, 3).Value = "abc"; // giá sai → error, không chặn cả file
        var tableSheet = workbook.Worksheets.Add("Ban");
        tableSheet.Cell(1, 1).Value = "Khu vực";
        tableSheet.Cell(2, 1).Value = "Tầng 1";
        tableSheet.Cell(2, 2).Value = "A01";
        tableSheet.Cell(2, 3).Value = "Bàn A1";
        tableSheet.Cell(3, 1).Value = "Tầng 1";
        tableSheet.Cell(3, 2).Value = "A02";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(stream.ToArray());
        fileContent.Headers.ContentType =
            new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "import.xlsx");

        var import = await owner.PostAsync($"/api/v1/venues/{venueId}/import", content);
        Assert.Equal(HttpStatusCode.OK, import.StatusCode);
        var result = await import.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(result.GetProperty("menuCreated").GetBoolean());
        Assert.True(result.GetProperty("menuPublished").GetBoolean());
        Assert.Equal(2, result.GetProperty("categoriesCreated").GetInt32());
        Assert.Equal(2, result.GetProperty("productsCreated").GetInt32());
        Assert.Equal(2, result.GetProperty("tablesCreated").GetInt32());
        Assert.Equal(1, result.GetProperty("errors").GetArrayLength()); // dòng giá sai
        Assert.Equal(2, result.GetProperty("newTables").GetArrayLength());

        // 3. Public menu thấy món + size + topping
        var guest = factory.CreateClient();
        var publicMenu = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/venues/{slug}/menu");
        var allProducts = publicMenu.GetProperty("categories").EnumerateArray()
            .SelectMany(c => c.GetProperty("products").EnumerateArray()).ToList();
        Assert.Equal(2, allProducts.Count);
        var bacXiu = allProducts.First(p => p.GetProperty("name").GetString() == "Bạc xỉu");
        Assert.Equal(2, bacXiu.GetProperty("variants").GetArrayLength());
        var traDao = allProducts.First(p => p.GetProperty("name").GetString() == "Trà đào");
        Assert.Equal(1, traDao.GetProperty("modifierGroups").GetArrayLength());

        // 4. QR bàn mới hoạt động
        var rawToken = result.GetProperty("newTables")[0].GetProperty("rawToken").GetString();
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/v1/public/q/{rawToken}")).StatusCode);

        // 5. Import lại cùng file → skip hết, không nhân đôi
        stream.Position = 0;
        var content2 = new MultipartFormDataContent();
        var fileContent2 = new ByteArrayContent(stream.ToArray());
        fileContent2.Headers.ContentType =
            new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content2.Add(fileContent2, "file", "import.xlsx");
        var again = await (await owner.PostAsync($"/api/v1/venues/{venueId}/import", content2))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, again.GetProperty("productsCreated").GetInt32());
        Assert.Equal(2, again.GetProperty("productsSkipped").GetInt32());
        Assert.Equal(2, again.GetProperty("tablesSkipped").GetInt32());
    }
}
