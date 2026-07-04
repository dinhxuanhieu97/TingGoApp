using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using Xunit;

namespace TingGo.IntegrationTests;

public class OpeningHoursTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    private async Task<(HttpClient Owner, Guid VenueId, string QrToken)> SetupAsync()
    {
        var owner = factory.CreateClient();
        var email = $"oh-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "OH" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán OH" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();
        var area = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/areas", new { name = "A" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tables = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/tables/bulk",
                new { areaId = area.GetProperty("id").GetGuid(), count = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        return (owner, venueId, tables[0].GetProperty("rawToken").GetString()!);
    }

    [Fact]
    public async Task PutGet_VaPublicIsOpenNow()
    {
        var (owner, venueId, qrToken) = await SetupAsync();

        // Chưa cấu hình → public không có trạng thái
        var guest = factory.CreateClient();
        var before = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/q/{qrToken}");
        Assert.Equal(JsonValueKind.Null, before.GetProperty("isOpenNow").ValueKind);

        // PUT mở cửa cả tuần 00:00–23:59 → đang mở
        var days = Enumerable.Range(1, 7)
            .Select(d => new { dayOfWeek = d, openTime = "00:00", closeTime = "23:59", isClosed = false });
        var put = await owner.PutAsJsonAsync($"/api/v1/venues/{venueId}/opening-hours", new { days });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(7, (await put.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength());

        var openNow = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/q/{qrToken}");
        Assert.True(openNow.GetProperty("isOpenNow").GetBoolean());
        Assert.Equal("00:00–23:59", openNow.GetProperty("todayHours").GetString());

        // PUT nghỉ cả tuần → ngoài giờ
        var closedDays = Enumerable.Range(1, 7)
            .Select(d => new { dayOfWeek = d, openTime = (string?)null, closeTime = (string?)null, isClosed = true });
        await owner.PutAsJsonAsync($"/api/v1/venues/{venueId}/opening-hours", new { days = closedDays });
        var closedNow = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/q/{qrToken}");
        Assert.False(closedNow.GetProperty("isOpenNow").GetBoolean());

        // Giờ sai định dạng → 400
        var bad = await owner.PutAsJsonAsync($"/api/v1/venues/{venueId}/opening-hours",
            new { days = new[] { new { dayOfWeek = 1, openTime = "bảy giờ", closeTime = "22:00", isClosed = false } } });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Import_SheetOpeningHours()
    {
        var (owner, venueId, qrToken) = await SetupAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("OpeningHours");
        sheet.Cell(1, 1).Value = "day_of_week";
        string[][] rows =
        [
            ["MONDAY", "00:00", "23:59", "FALSE"], ["TUESDAY", "00:00", "23:59", "FALSE"],
            ["WEDNESDAY", "00:00", "23:59", "FALSE"], ["THURSDAY", "00:00", "23:59", "FALSE"],
            ["FRIDAY", "00:00", "23:59", "FALSE"], ["SATURDAY", "00:00", "23:59", "FALSE"],
            ["SUNDAY", "00:00", "23:59", "FALSE"],
        ];
        for (var i = 0; i < rows.Length; i++)
            for (var c = 0; c < 4; c++)
                sheet.Cell(i + 2, c + 1).Value = rows[i][c];

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(stream.ToArray());
        fileContent.Headers.ContentType =
            new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "hours.xlsx");

        var created = await owner.PostAsync($"/api/v1/venues/{venueId}/imports", content);
        var summary = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("READY_TO_IMPORT", summary.GetProperty("status").GetString());
        var importId = summary.GetProperty("importId").GetGuid();
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync($"/api/v1/imports/{importId}/commit", null)).StatusCode);

        var guest = factory.CreateClient();
        var qr = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/public/q/{qrToken}");
        Assert.True(qr.GetProperty("isOpenNow").GetBoolean());
    }
}
