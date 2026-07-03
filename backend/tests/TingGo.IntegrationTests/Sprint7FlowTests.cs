using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Sprint 7: service request + bill + đóng/mở bàn + audit.</summary>
public class Sprint7FlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    private async Task<(HttpClient Owner, HttpClient Guest, Guid VenueId, string SessionToken, Guid ProductId)>
        SetupAsync()
    {
        var owner = factory.CreateClient();
        var email = $"s7-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());

        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "S7" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán S7" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();

        var menu = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/menus", new { name = "M" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var category = await (await owner.PostAsJsonAsync(
                $"/api/v1/menus/{menu.GetProperty("id").GetGuid()}/categories", new { name = "C" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var product = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/products",
                new { categoryId = category.GetProperty("id").GetGuid(), name = "Món S7", basePriceMinor = 10000 }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var area = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/areas", new { name = "A" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tables = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/tables/bulk",
                new { areaId = area.GetProperty("id").GetGuid(), count = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var guest = factory.CreateClient();
        var session = await (await guest.PostAsJsonAsync("/api/v1/public/table-sessions",
                new { qrToken = tables[0].GetProperty("rawToken").GetString() }))
            .Content.ReadFromJsonAsync<JsonElement>();

        return (owner, guest, venueId,
            session.GetProperty("sessionToken").GetString()!,
            product.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ServiceRequest_TaoVaXuLy()
    {
        var (owner, guest, venueId, sessionToken, _) = await SetupAsync();

        // Khách gọi nhân viên
        var created = await guest.PostAsJsonAsync("/api/v1/public/service-requests",
            new { sessionToken, type = "call_staff", note = "Xin thêm đá" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var requestId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Type sai → 400
        var badType = await guest.PostAsJsonAsync("/api/v1/public/service-requests",
            new { sessionToken, type = "karaoke" });
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);

        // Quán thấy trong danh sách pending
        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/service-requests");
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("id").GetGuid() == requestId);

        // acknowledge → resolve; resolve lần 2 → 409
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync($"/api/v1/service-requests/{requestId}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await owner.PostAsync($"/api/v1/service-requests/{requestId}/resolve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsync($"/api/v1/service-requests/{requestId}/resolve", null)).StatusCode);
    }

    [Fact]
    public async Task Bill_DongBan_MoLaiBan()
    {
        var (owner, guest, venueId, sessionToken, productId) = await SetupAsync();

        // Khách order 2 món
        for (var i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/orders")
            {
                Content = JsonContent.Create(new
                {
                    sessionToken,
                    clientOrderId = Guid.NewGuid(),
                    items = new[] { new { productId, quantity = 1, optionIds = Array.Empty<Guid>() } },
                }),
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            await guest.SendAsync(request);
        }

        // Bill: 2 order, 20000
        var sessions = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/table-sessions");
        var session = sessions[0];
        var sessionId = session.GetProperty("id").GetGuid();
        Assert.Equal(20000, session.GetProperty("totalMinor").GetInt64());

        // Đóng bàn khi còn order chưa xong → 409
        var rowVersion = session.GetProperty("rowVersion").GetInt64();
        var earlyClose = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/close",
            new { rowVersion });
        Assert.Equal(HttpStatusCode.Conflict, earlyClose.StatusCode);

        // Hoàn thành 2 order (confirm → preparing → ready → complete)
        var active = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/orders/active");
        foreach (var entry in active.EnumerateArray())
        {
            var orderId = entry.GetProperty("view").GetProperty("id").GetGuid();
            var rv = entry.GetProperty("view").GetProperty("rowVersion").GetInt64();
            foreach (var action in new[] { "confirm", "start-preparing", "mark-ready", "complete" })
            {
                var resp = await owner.PostAsJsonAsync($"/api/v1/orders/{orderId}/{action}", new { rowVersion = rv });
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
                rv = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rowVersion").GetInt64();
            }
        }

        // Đóng bàn thành công
        var close = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/close", new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, close.StatusCode);
        var closed = await close.Content.ReadFromJsonAsync<JsonElement>();

        // Order mới vào phiên đã đóng → 410
        var lateOrder = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/orders")
        {
            Content = JsonContent.Create(new
            {
                sessionToken,
                clientOrderId = Guid.NewGuid(),
                items = new[] { new { productId, quantity = 1, optionIds = Array.Empty<Guid>() } },
            }),
        };
        lateOrder.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Gone, (await guest.SendAsync(lateOrder)).StatusCode);

        // Owner mở lại bàn được
        var reopen = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/reopen",
            new { rowVersion = closed!.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.OK, reopen.StatusCode);

        // Waiter không được mở lại bàn (đóng lại trước, rồi thử reopen bằng waiter)
        var reopened = await reopen.Content.ReadFromJsonAsync<JsonElement>();
        await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/close",
            new { rowVersion = reopened!.GetProperty("rowVersion").GetInt64() });

        var staff = await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/staff",
            new { displayName = "Waiter S7", role = "waiter", pin = "1234" });
        var staffCode = (await staff.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("staffCode").GetString();
        var waiter = factory.CreateClient();
        var waiterTokens = await (await waiter.PostAsJsonAsync("/api/v1/auth/staff/login",
                new { venueId, staffCode, pin = "1234" })).Content.ReadFromJsonAsync<JsonElement>();
        waiter.DefaultRequestHeaders.Authorization =
            new("Bearer", waiterTokens.GetProperty("accessToken").GetString());

        var forbidden = await waiter.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/reopen",
            new { rowVersion = 99 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
