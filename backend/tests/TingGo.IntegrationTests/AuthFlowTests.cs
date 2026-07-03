using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TingGo.Modules.Identity.Auth;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Capture email thay vì gửi SMTP thật trong test.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    public string? LastBody { get; private set; }

    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        LastBody = body;
        return Task.CompletedTask;
    }
}

public sealed class AuthWebAppFactory : WebApplicationFactory<Program>
{
    public FakeEmailSender EmailSender { get; } = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);
        });
    }
}

public class AuthFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    // Yêu cầu Postgres (docker compose) đang chạy — DoD: integration test khi chạm DB.
    [Fact]
    public async Task OtpFlow_DangNhap_TaoOrg_TaoVenue_ThanhCong()
    {
        var client = factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@tinggo.local";

        // 1. Request OTP
        var reqResp = await client.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        Assert.Equal(HttpStatusCode.Accepted, reqResp.StatusCode);
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;

        // 2. Verify → tokens
        var verifyResp = await client.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new { email, code, deviceName = "test" });
        Assert.Equal(HttpStatusCode.OK, verifyResp.StatusCode);
        var tokens = await verifyResp.Content.ReadFromJsonAsync<AuthTokens>();
        Assert.NotNull(tokens);

        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens!.AccessToken);

        // 3. Sai OTP không dùng lại được
        var reuse = await client.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code });
        Assert.Equal(HttpStatusCode.BadRequest, reuse.StatusCode);

        // 4. Onboarding: org + venue
        var orgResp = await client.PostAsJsonAsync("/api/v1/organizations", new { name = "Quán Test" });
        Assert.Equal(HttpStatusCode.Created, orgResp.StatusCode);
        var org = await orgResp.Content.ReadFromJsonAsync<OrgDto>();

        var venueResp = await client.PostAsJsonAsync($"/api/v1/organizations/{org!.Id}/venues",
            new { name = "Chi nhánh Test" });
        Assert.Equal(HttpStatusCode.Created, venueResp.StatusCode);

        // 5. Tenant isolation: user khác không xem được org này
        var otherClient = factory.CreateClient();
        var otherEmail = $"other-{Guid.NewGuid():N}@tinggo.local";
        await otherClient.PostAsJsonAsync("/api/v1/auth/otp/request", new { email = otherEmail });
        var otherCode = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var otherVerify = await otherClient.PostAsJsonAsync("/api/v1/auth/otp/verify",
            new { email = otherEmail, code = otherCode });
        var otherTokens = await otherVerify.Content.ReadFromJsonAsync<AuthTokens>();
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", otherTokens!.AccessToken);

        var forbidden = await otherClient.GetAsync($"/api/v1/organizations/{org.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    private sealed record OrgDto(Guid Id, string Name);
}
