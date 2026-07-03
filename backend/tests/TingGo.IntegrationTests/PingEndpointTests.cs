using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TingGo.IntegrationTests;

public class PingEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Ping_TraVe200()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/ping");

        response.EnsureSuccessStatusCode();
    }
}
