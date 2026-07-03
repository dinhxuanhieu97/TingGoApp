using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

public class CatalogFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task MenuFlow_TaoMenu_DanhMuc_Mon_Modifier_Publish()
    {
        // Setup: owner + org + venue
        var client = factory.CreateClient();
        var email = $"catalog-{Guid.NewGuid():N}@tinggo.local";
        await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<TokenDto>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        var org = await (await client.PostAsJsonAsync("/api/v1/organizations", new { name = "Org Catalog" }))
            .Content.ReadFromJsonAsync<IdDto>();
        var venue = await (await client.PostAsJsonAsync($"/api/v1/organizations/{org!.Id}/venues",
            new { name = "Quán Catalog" })).Content.ReadFromJsonAsync<IdDto>();

        // 1. Menu draft — publish khi chưa có danh mục phải fail
        var menu = await (await client.PostAsJsonAsync($"/api/v1/venues/{venue!.Id}/menus",
            new { name = "Menu chính" })).Content.ReadFromJsonAsync<IdDto>();
        var earlyPublish = await client.PostAsync($"/api/v1/menus/{menu!.Id}/publish", null);
        Assert.Equal(HttpStatusCode.BadRequest, earlyPublish.StatusCode);

        // 2. Danh mục + món
        var category = await (await client.PostAsJsonAsync($"/api/v1/menus/{menu.Id}/categories",
            new { name = "Cà phê" })).Content.ReadFromJsonAsync<IdDto>();

        var productResp = await client.PostAsJsonAsync($"/api/v1/venues/{venue.Id}/products",
            new { categoryId = category!.Id, name = "Cà phê sữa", basePriceMinor = 25000 });
        Assert.Equal(HttpStatusCode.Created, productResp.StatusCode);
        var productJson = await productResp.Content.ReadFromJsonAsync<JsonElement>();
        var productId = productJson.GetProperty("id").GetGuid();
        Assert.Equal("VND", productJson.GetProperty("currencyCode").GetString());

        // 3. Giá âm bị chặn
        var negative = await client.PostAsJsonAsync($"/api/v1/venues/{venue.Id}/products",
            new { categoryId = category.Id, name = "Món lỗi", basePriceMinor = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);

        // 4. Variant + modifier group/option + gán vào món
        var variant = await client.PostAsJsonAsync($"/api/v1/products/{productId}/variants",
            new { name = "Size L", priceDeltaMinor = 5000, isDefault = true });
        Assert.Equal(HttpStatusCode.Created, variant.StatusCode);

        var groupResp = await client.PostAsJsonAsync($"/api/v1/venues/{venue.Id}/modifier-groups",
            new { name = "Mức đường", minSelect = 0, maxSelect = 1, isRequired = false });
        var group = await groupResp.Content.ReadFromJsonAsync<IdDto>();
        await client.PostAsJsonAsync($"/api/v1/modifier-groups/{group!.Id}/options",
            new { name = "Ít đường", priceDeltaMinor = 0 });

        var assign = await client.PutAsJsonAsync($"/api/v1/products/{productId}/modifier-groups",
            new { modifierGroupIds = new[] { group.Id } });
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        // 5. Bật/tắt món nhanh (MOB-04)
        var toggle = await client.PatchAsJsonAsync($"/api/v1/products/{productId}/availability",
            new { isAvailable = false });
        Assert.Equal(HttpStatusCode.OK, toggle.StatusCode);

        // 6. Publish thành công khi đã có danh mục
        var publish = await client.PostAsync($"/api/v1/menus/{menu.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);

        // 7. Tenant isolation: user khác không xem được menu
        var stranger = factory.CreateClient();
        var strangerEmail = $"stranger-{Guid.NewGuid():N}@tinggo.local";
        await stranger.PostAsJsonAsync("/api/v1/auth/otp/request", new { email = strangerEmail });
        var strangerCode = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var strangerTokens = await (await stranger.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new { email = strangerEmail, code = strangerCode })).Content.ReadFromJsonAsync<TokenDto>();
        stranger.DefaultRequestHeaders.Authorization = new("Bearer", strangerTokens!.AccessToken);
        var forbidden = await stranger.GetAsync($"/api/v1/menus/{menu.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private sealed record TokenDto(string AccessToken, string RefreshToken);
    private sealed record IdDto(Guid Id);
}
