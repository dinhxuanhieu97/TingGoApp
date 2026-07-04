using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

public class QrPosterTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task RegenerateAll_TokenMoiHoatDong_TokenCuBiThuHoi()
    {
        var owner = factory.CreateClient();
        var email = $"poster-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());
        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Poster" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán Poster" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();
        var area = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/areas", new { name = "A" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tables = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/tables/bulk",
                new { areaId = area.GetProperty("id").GetGuid(), count = 3 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var oldToken = tables[0].GetProperty("rawToken").GetString();

        // Khóa 1 bàn — bàn khóa không có trong poster
        var disabledId = tables[2].GetProperty("id").GetGuid();
        await owner.PostAsync($"/api/v1/tables/{disabledId}/disable", null);

        var regen = await owner.PostAsync($"/api/v1/venues/{venueId}/tables/qr/regenerate-all", null);
        Assert.Equal(HttpStatusCode.OK, regen.StatusCode);
        var posters = await regen.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, posters.GetArrayLength()); // chỉ bàn active

        // Token cũ 410, URL mới 200
        var guest = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Gone, (await guest.GetAsync($"/api/v1/public/q/{oldToken}")).StatusCode);
        var newUrl = posters[0].GetProperty("qrUrl").GetString()!;
        var newToken = newUrl[(newUrl.LastIndexOf('/') + 1)..];
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/v1/public/q/{newToken}")).StatusCode);
    }
}
