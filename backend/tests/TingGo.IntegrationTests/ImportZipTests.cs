using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Sprint Import 3: gói ZIP có hình ảnh — AC 8 + PRD 11.1.</summary>
public class ImportZipTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    // PNG 1x1 hợp lệ
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private async Task<(HttpClient Owner, Guid VenueId)> SetupVenueAsync()
    {
        var owner = factory.CreateClient();
        var email = $"zip-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Zip" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán Zip" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return (owner, venue.GetProperty("id").GetGuid());
    }

    private static byte[] BuildXlsx(string image1, string image2)
    {
        using var workbook = new XLWorkbook();
        var categories = workbook.Worksheets.Add("Categories");
        categories.Cell(1, 1).Value = "category_code";
        categories.Cell(2, 1).Value = "COFFEE"; categories.Cell(2, 2).Value = "Cà phê";
        var products = workbook.Worksheets.Add("Products");
        products.Cell(1, 1).Value = "product_code";
        products.Cell(2, 1).Value = "CF_SUA"; products.Cell(2, 2).Value = "COFFEE";
        products.Cell(2, 3).Value = "Cà phê sữa"; products.Cell(2, 5).Value = 25000;
        products.Cell(2, 6).Value = image1; // ảnh có trong zip
        products.Cell(3, 1).Value = "CF_DEN"; products.Cell(3, 2).Value = "COFFEE";
        products.Cell(3, 3).Value = "Cà phê đen"; products.Cell(3, 5).Value = 20000;
        products.Cell(3, 6).Value = image2; // ảnh KHÔNG có trong zip
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static MultipartFormDataContent ZipMultipart(Action<ZipArchive> build)
    {
        var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            build(archive);
        }
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipStream.ToArray());
        fileContent.Headers.ContentType = new("application/zip");
        content.Add(fileContent, "file", "tinggo-import.zip");
        return content;
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    [Fact]
    public async Task ZipImport_GanAnh_AnhThieuVanTaoMon()
    {
        var (owner, venueId) = await SetupVenueAsync();

        var upload = await owner.PostAsync($"/api/v1/venues/{venueId}/imports",
            ZipMultipart(archive =>
            {
                AddEntry(archive, "menu.xlsx", BuildXlsx("images/ca-phe-sua.png", "khong-ton-tai.jpg"));
                AddEntry(archive, "images/ca-phe-sua.png", TinyPng);
                AddEntry(archive, "images/thua.png", TinyPng); // không món nào tham chiếu
            }));
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var summary = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("READY_TO_IMPORT", summary.GetProperty("status").GetString());
        var importId = summary.GetProperty("importId").GetGuid();

        // Cảnh báo ảnh thiếu + info ảnh thừa
        var issues = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{importId}/issues");
        var list = issues.EnumerateArray().ToList();
        Assert.Contains(list, i => i.GetProperty("code").GetString() == "IMPORT_IMAGE_NOT_FOUND");
        Assert.Contains(list, i => i.GetProperty("code").GetString() == "IMPORT_IMAGE_UNUSED");

        // Commit: 1 ảnh gắn thành công; món thiếu ảnh vẫn tạo (AC 8)
        var commit = await owner.PostAsync($"/api/v1/imports/{importId}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var outcome = (await commit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("outcome");
        Assert.Equal(2, outcome.GetProperty("productsCreated").GetInt32());
        Assert.Equal(1, outcome.GetProperty("imagesAttached").GetInt32());
        Assert.Equal(0, outcome.GetProperty("imagesFailed").GetInt32());

        var products = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/products");
        var withImage = products.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "Cà phê sữa");
        Assert.StartsWith("/files/images/", withImage.GetProperty("imageUrl").GetString());
        var withoutImage = products.EnumerateArray()
            .First(p => p.GetProperty("name").GetString() == "Cà phê đen");
        Assert.Equal(JsonValueKind.Null, withoutImage.GetProperty("imageUrl").ValueKind);
    }

    [Fact]
    public async Task ZipImport_ChanFileNguyHiem()
    {
        var (owner, venueId) = await SetupVenueAsync();

        // Path traversal
        var traversal = await owner.PostAsync($"/api/v1/venues/{venueId}/imports",
            ZipMultipart(archive =>
            {
                AddEntry(archive, "menu.xlsx", BuildXlsx("", ""));
                AddEntry(archive, "../../../etc/evil.png", TinyPng);
            }));
        Assert.Equal(HttpStatusCode.BadRequest, traversal.StatusCode);

        // File thực thi
        var executable = await owner.PostAsync($"/api/v1/venues/{venueId}/imports",
            ZipMultipart(archive =>
            {
                AddEntry(archive, "menu.xlsx", BuildXlsx("", ""));
                AddEntry(archive, "virus.exe", [0x4D, 0x5A]);
            }));
        Assert.Equal(HttpStatusCode.BadRequest, executable.StatusCode);

        // Ảnh giả (đuôi .png nhưng không phải ảnh) → WARNING bỏ qua, không chặn
        var fakeImage = await owner.PostAsync($"/api/v1/venues/{venueId}/imports",
            ZipMultipart(archive =>
            {
                AddEntry(archive, "menu.xlsx", BuildXlsx("gia.png", ""));
                AddEntry(archive, "images/gia.png", "not an image"u8.ToArray());
            }));
        Assert.Equal(HttpStatusCode.Created, fakeImage.StatusCode);
        var summary = await fakeImage.Content.ReadFromJsonAsync<JsonElement>();
        var importId = summary.GetProperty("importId").GetGuid();
        var issues = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/imports/{importId}/issues");
        Assert.Contains(issues.EnumerateArray(),
            i => i.GetProperty("code").GetString() == "IMPORT_IMAGE_INVALID");
    }
}
