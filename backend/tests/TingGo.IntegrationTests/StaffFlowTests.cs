using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TingGo.IntegrationTests;

public class StaffFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task TaoNhanVien_StaffLogin_VaPhanQuyen()
    {
        // Owner đăng nhập + tạo org/venue
        var owner = factory.CreateClient();
        var email = $"owner-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<TokenDto>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Org Staff Test" }))
            .Content.ReadFromJsonAsync<IdDto>();
        var venue = await (await owner.PostAsJsonAsync($"/api/v1/organizations/{org!.Id}/venues",
            new { name = "Quán Staff Test" })).Content.ReadFromJsonAsync<VenueDto>();

        // Tạo nhân viên waiter, PIN 1234
        var staffResp = await owner.PostAsJsonAsync($"/api/v1/venues/{venue!.Id}/staff",
            new { displayName = "Anh Ba", role = "waiter", pin = "1234" });
        Assert.Equal(HttpStatusCode.Created, staffResp.StatusCode);
        var staff = await staffResp.Content.ReadFromJsonAsync<StaffDto>();
        Assert.Matches(@"^NV\d+", staff!.StaffCode);

        // Staff login đúng PIN
        var staffClient = factory.CreateClient();
        var loginResp = await staffClient.PostAsJsonAsync("/api/v1/auth/staff/login",
            new { venueId = venue.Id, staffCode = staff.StaffCode, pin = "1234" });
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var staffTokens = await loginResp.Content.ReadFromJsonAsync<TokenDto>();

        // Sai PIN → 401
        var badPin = await staffClient.PostAsJsonAsync("/api/v1/auth/staff/login",
            new { venueId = venue.Id, staffCode = staff.StaffCode, pin = "9999" });
        Assert.Equal(HttpStatusCode.Unauthorized, badPin.StatusCode);

        // Waiter không được tạo nhân viên khác → 403
        staffClient.DefaultRequestHeaders.Authorization = new("Bearer", staffTokens!.AccessToken);
        var forbidden = await staffClient.PostAsJsonAsync($"/api/v1/venues/{venue.Id}/staff",
            new { displayName = "Ai Đó", role = "waiter", pin = "5678" });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // PATCH venue với rowVersion đúng → OK, rowVersion cũ → 409
        var patchOk = await owner.PatchAsJsonAsync($"/api/v1/venues/{venue.Id}",
            new { name = "Quán Đổi Tên", rowVersion = venue.RowVersion });
        Assert.Equal(HttpStatusCode.OK, patchOk.StatusCode);

        var patchStale = await owner.PatchAsJsonAsync($"/api/v1/venues/{venue.Id}",
            new { name = "Quán Đổi Nữa", rowVersion = venue.RowVersion });
        Assert.Equal(HttpStatusCode.Conflict, patchStale.StatusCode);
    }

    private sealed record TokenDto(string AccessToken, string RefreshToken);
    private sealed record IdDto(Guid Id);
    private sealed record VenueDto(Guid Id, long RowVersion);
    private sealed record StaffDto(Guid MembershipId, Guid UserId, string DisplayName, string Role, string StaffCode);
}
