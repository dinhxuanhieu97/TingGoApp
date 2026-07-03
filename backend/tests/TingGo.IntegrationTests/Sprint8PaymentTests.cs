using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Sprint 8: thanh toán cash/CK (ADR-004) + device tokens.</summary>
public class Sprint8PaymentTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    [Fact]
    public async Task Payment_TaoThuXacNhan_VaValidation()
    {
        // Setup nhanh: venue + bàn + 1 order 10000
        var owner = factory.CreateClient();
        var email = $"s8-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());

        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "S8" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán S8" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();
        var menu = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/menus", new { name = "M" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var category = await (await owner.PostAsJsonAsync(
                $"/api/v1/menus/{menu.GetProperty("id").GetGuid()}/categories", new { name = "C" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var product = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/products",
                new { categoryId = category.GetProperty("id").GetGuid(), name = "Món", basePriceMinor = 10000 }))
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
        var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/orders")
        {
            Content = JsonContent.Create(new
            {
                sessionToken = session.GetProperty("sessionToken").GetString(),
                clientOrderId = Guid.NewGuid(),
                items = new[]
                {
                    new { productId = product.GetProperty("id").GetGuid(), quantity = 1, optionIds = Array.Empty<Guid>() },
                },
            }),
        };
        orderRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await guest.SendAsync(orderRequest);

        var sessions = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/venues/{venueId}/table-sessions");
        var sessionId = sessions[0].GetProperty("id").GetGuid();

        // Thu vượt bill → 400
        var over = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/payments",
            new { method = "cash", amountMinor = 99999 });
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);

        // Method sai → 400
        var badMethod = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/payments",
            new { method = "bitcoin" });
        Assert.Equal(HttpStatusCode.BadRequest, badMethod.StatusCode);

        // Tạo payment mặc định = bill (10000) → confirm → paid
        var created = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/payments",
            new { method = "cash" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payment = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(10000, payment.GetProperty("amountMinor").GetInt64());
        var paymentId = payment.GetProperty("id").GetGuid();

        var confirm = await owner.PostAsync($"/api/v1/payments/{paymentId}/confirm-cash", null);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("paid",
            (await confirm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // Confirm lần 2 → 409; đã thu đủ → tạo thêm payment → 400
        Assert.Equal(HttpStatusCode.Conflict,
            (await owner.PostAsync($"/api/v1/payments/{paymentId}/confirm-cash", null)).StatusCode);
        var extra = await owner.PostAsJsonAsync($"/api/v1/table-sessions/{sessionId}/payments",
            new { method = "cash" });
        Assert.Equal(HttpStatusCode.BadRequest, extra.StatusCode);

        // Tổng hợp phiên: paid = bill, remaining = 0
        var summary = await owner.GetFromJsonAsync<JsonElement>(
            $"/api/v1/table-sessions/{sessionId}/payments");
        Assert.Equal(0, summary.GetProperty("remainingMinor").GetInt64());

        // Device tokens
        var device = await owner.PostAsJsonAsync("/api/v1/me/devices",
            new { platform = "android", token = $"fcm-{Guid.NewGuid():N}", deviceName = "Pixel test" });
        Assert.Equal(HttpStatusCode.Created, device.StatusCode);
        var devices = await owner.GetFromJsonAsync<JsonElement>("/api/v1/me/devices");
        Assert.True(devices.GetArrayLength() >= 1);
    }
}
