using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

public class QrPublicFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task QrFlow_TaoBan_QuetQr_XemPublicMenu()
    {
        // Setup: owner + org + venue + menu published
        var owner = factory.CreateClient();
        var email = $"qr-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<TokenDto>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Org QR" }))
            .Content.ReadFromJsonAsync<IdDto>();
        var venueJson = await (await owner.PostAsJsonAsync($"/api/v1/organizations/{org!.Id}/venues",
            new { name = "Quán QR Test" })).Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venueJson.GetProperty("id").GetGuid();
        var slug = venueJson.GetProperty("slug").GetString();

        var menu = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/menus",
            new { name = "Menu" })).Content.ReadFromJsonAsync<IdDto>();
        var category = await (await owner.PostAsJsonAsync($"/api/v1/menus/{menu!.Id}/categories",
            new { name = "Đồ uống" })).Content.ReadFromJsonAsync<IdDto>();
        await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/products",
            new { categoryId = category!.Id, name = "Trà đào", basePriceMinor = 35000 });
        await owner.PostAsync($"/api/v1/menus/{menu.Id}/publish", null);

        // 1. Tạo khu vực + bàn hàng loạt (có QR)
        var area = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/areas",
            new { name = "Tầng 1" })).Content.ReadFromJsonAsync<IdDto>();
        var bulkResp = await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/tables/bulk",
            new { areaId = area!.Id, count = 3 });
        Assert.Equal(HttpStatusCode.OK, bulkResp.StatusCode);
        var tables = await bulkResp.Content.ReadFromJsonAsync<List<TableCreatedDto>>();
        Assert.Equal(3, tables!.Count);
        Assert.All(tables, t => Assert.False(string.IsNullOrEmpty(t.RawToken)));

        // 2. Khách quét QR (không auth)
        var guest = factory.CreateClient();
        var qrResp = await guest.GetAsync($"/api/v1/public/q/{tables[0].RawToken}");
        Assert.Equal(HttpStatusCode.OK, qrResp.StatusCode);
        var qrData = await qrResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("T01", qrData.GetProperty("table").GetProperty("code").GetString());
        Assert.Equal(slug, qrData.GetProperty("venue").GetProperty("slug").GetString());

        // 3. Public menu
        var menuResp = await guest.GetAsync($"/api/v1/public/venues/{slug}/menu");
        Assert.Equal(HttpStatusCode.OK, menuResp.StatusCode);
        var menuData = await menuResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstCategory = menuData.GetProperty("categories")[0];
        Assert.Equal("Đồ uống", firstCategory.GetProperty("name").GetString());
        Assert.Equal("Trà đào",
            firstCategory.GetProperty("products")[0].GetProperty("name").GetString());

        // 4. Regenerate QR → token cũ bị thu hồi (410 QR_REVOKED)
        var regen = await owner.PostAsync($"/api/v1/tables/{tables[0].Id}/qr/regenerate", null);
        Assert.Equal(HttpStatusCode.OK, regen.StatusCode);
        var oldToken = await guest.GetAsync($"/api/v1/public/q/{tables[0].RawToken}");
        Assert.Equal(HttpStatusCode.Gone, oldToken.StatusCode);
        var error = await oldToken.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QR_REVOKED", error.GetProperty("code").GetString());

        // 5. Token mới hoạt động; bàn bị khóa → TABLE_DISABLED
        var newToken = (await regen.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("rawToken").GetString();
        Assert.Equal(HttpStatusCode.OK,
            (await guest.GetAsync($"/api/v1/public/q/{newToken}")).StatusCode);

        await owner.PostAsync($"/api/v1/tables/{tables[0].Id}/disable", null);
        var disabled = await guest.GetAsync($"/api/v1/public/q/{newToken}");
        Assert.Equal(HttpStatusCode.Gone, disabled.StatusCode);
    }

    private sealed record TokenDto(string AccessToken, string RefreshToken);
    private sealed record IdDto(Guid Id);
    private sealed record TableCreatedDto(Guid Id, Guid AreaId, string Code, string Name, string QrUrl, string RawToken);
}
