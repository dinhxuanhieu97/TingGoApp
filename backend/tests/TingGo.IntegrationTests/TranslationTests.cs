using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>i18n: import sheet Translations + public menu theo ?lang=.</summary>
public class TranslationTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task Import_BanDich_PublicMenuTheoNgonNgu()
    {
        var owner = factory.CreateClient();
        var email = $"i18n-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "i18n" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán i18n" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();
        var slug = venue.GetProperty("slug").GetString();

        // File: 1 danh mục + 1 món + bản dịch en-US, và 1 dòng dịch VENUE (INFO skip)
        using var workbook = new XLWorkbook();
        var categories = workbook.Worksheets.Add("Categories");
        categories.Cell(1, 1).Value = "category_code";
        categories.Cell(2, 1).Value = "COFFEE"; categories.Cell(2, 2).Value = "Cà phê";
        var products = workbook.Worksheets.Add("Products");
        products.Cell(1, 1).Value = "product_code";
        products.Cell(2, 1).Value = "CF_SUA"; products.Cell(2, 2).Value = "COFFEE";
        products.Cell(2, 3).Value = "Cà phê sữa"; products.Cell(2, 4).Value = "Phin với sữa đặc";
        products.Cell(2, 5).Value = 25000;
        var translations = workbook.Worksheets.Add("Translations");
        translations.Cell(1, 1).Value = "entity_type";
        translations.Cell(2, 1).Value = "PRODUCT"; translations.Cell(2, 2).Value = "CF_SUA";
        translations.Cell(2, 3).Value = "en-US"; translations.Cell(2, 4).Value = "Vietnamese Milk Coffee";
        translations.Cell(2, 5).Value = "Phin coffee with condensed milk";
        translations.Cell(3, 1).Value = "VENUE"; translations.Cell(3, 2).Value = "X";
        translations.Cell(3, 3).Value = "en-US"; translations.Cell(3, 4).Value = "Name";

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(stream.ToArray());
        fileContent.Headers.ContentType =
            new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "i18n.xlsx");

        var created = await owner.PostAsync($"/api/v1/venues/{venueId}/imports", content);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("READY_TO_IMPORT", summary.GetProperty("status").GetString());
        var importId = summary.GetProperty("importId").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync($"/api/v1/imports/{importId}/commit", null)).StatusCode);

        // Công bố menu (import để nháp)
        var menus = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/menus");
        await owner.PostAsync($"/api/v1/menus/{menus[0].GetProperty("id").GetGuid()}/publish", null);

        var guest = factory.CreateClient();

        // Mặc định (vi) → tên gốc
        var viMenu = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/venues/{slug}/menu");
        var viProduct = viMenu.GetProperty("categories")[0].GetProperty("products")[0];
        Assert.Equal("Cà phê sữa", viProduct.GetProperty("name").GetString());

        // ?lang=en-US → bản dịch
        var enMenu = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/venues/{slug}/menu?lang=en-US");
        var enProduct = enMenu.GetProperty("categories")[0].GetProperty("products")[0];
        Assert.Equal("Vietnamese Milk Coffee", enProduct.GetProperty("name").GetString());
        Assert.Equal("Phin coffee with condensed milk", enProduct.GetProperty("description").GetString());

        // ?lang=en (prefix match) → vẫn ra bản dịch en-US
        var enShort = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/venues/{slug}/menu?lang=en");
        Assert.Equal("Vietnamese Milk Coffee",
            enShort.GetProperty("categories")[0].GetProperty("products")[0].GetProperty("name").GetString());

        // ?lang=fr (không có bản dịch) → fallback tên gốc
        var frMenu = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/venues/{slug}/menu?lang=fr");
        Assert.Equal("Cà phê sữa",
            frMenu.GetProperty("categories")[0].GetProperty("products")[0].GetProperty("name").GetString());
    }
}
