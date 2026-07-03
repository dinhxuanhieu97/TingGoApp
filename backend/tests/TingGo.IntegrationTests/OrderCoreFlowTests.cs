using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace TingGo.IntegrationTests;

/// <summary>Test plan I1–I14 (rút gọn các case chính) — KPI: 0% đơn trùng, 0% mất đơn.</summary>
public class OrderCoreFlowTests(AuthWebAppFactory factory) : IClassFixture<AuthWebAppFactory>
{
    private sealed record Setup(
        HttpClient Owner, HttpClient Guest, Guid VenueId, string QrToken,
        string SessionToken, Guid ProductId, long RowVersionOfProductPrice);

    private async Task<Setup> SetupVenueWithMenuAndTableAsync()
    {
        var owner = factory.CreateClient();
        var email = $"oc-{Guid.NewGuid():N}@tinggo.local";
        await owner.PostAsJsonAsync("/api/v1/auth/otp/request", new { email });
        var code = Regex.Match(factory.EmailSender.LastBody!, @"\d{6}").Value;
        var tokens = await (await owner.PostAsJsonAsync("/api/v1/auth/otp/verify", new { email, code }))
            .Content.ReadFromJsonAsync<JsonElement>();
        owner.DefaultRequestHeaders.Authorization = new("Bearer", tokens.GetProperty("accessToken").GetString());

        var org = await (await owner.PostAsJsonAsync("/api/v1/organizations", new { name = "Org OC" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venue = await (await owner.PostAsJsonAsync(
                $"/api/v1/organizations/{org.GetProperty("id").GetGuid()}/venues", new { name = "Quán OC" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var venueId = venue.GetProperty("id").GetGuid();

        var menu = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/menus", new { name = "M" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var category = await (await owner.PostAsJsonAsync(
                $"/api/v1/menus/{menu.GetProperty("id").GetGuid()}/categories", new { name = "Đồ uống" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var product = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/products",
                new { categoryId = category.GetProperty("id").GetGuid(), name = "Cà phê", basePriceMinor = 20000 }))
            .Content.ReadFromJsonAsync<JsonElement>();

        var area = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/areas", new { name = "A" }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var tables = await (await owner.PostAsJsonAsync($"/api/v1/venues/{venueId}/tables/bulk",
                new { areaId = area.GetProperty("id").GetGuid(), count = 1 }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var qrToken = tables[0].GetProperty("rawToken").GetString()!;

        var guest = factory.CreateClient();
        var session = await (await guest.PostAsJsonAsync("/api/v1/public/table-sessions",
                new { qrToken })).Content.ReadFromJsonAsync<JsonElement>();

        return new Setup(owner, guest, venueId, qrToken,
            session.GetProperty("sessionToken").GetString()!,
            product.GetProperty("id").GetGuid(),
            product.GetProperty("rowVersion").GetInt64());
    }

    private static HttpRequestMessage OrderRequest(string sessionToken, Guid clientOrderId, Guid productId,
        string idempotencyKey, int quantity = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/orders")
        {
            Content = JsonContent.Create(new
            {
                sessionToken,
                clientOrderId,
                items = new[] { new { productId, quantity, optionIds = Array.Empty<Guid>() } },
            }),
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        return request;
    }

    [Fact] // I1 + I2 + I3: idempotency — KPI 0% trùng
    public async Task Idempotency_GuiLaiRequest_KhongTaoDonTrung()
    {
        var setup = await SetupVenueWithMenuAndTableAsync();
        var clientOrderId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        // Lần 1: 201
        var first = await setup.Guest.SendAsync(
            OrderRequest(setup.SessionToken, clientOrderId, setup.ProductId, key));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstOrder = await first.Content.ReadFromJsonAsync<JsonElement>();

        // I1: retry cùng key + cùng body → cùng order (id + orderNumber), không tạo đơn mới
        var retry = await setup.Guest.SendAsync(
            OrderRequest(setup.SessionToken, clientOrderId, setup.ProductId, key));
        var retryOrder = await retry.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstOrder.GetProperty("id").GetGuid(), retryOrder.GetProperty("id").GetGuid());
        Assert.Equal(firstOrder.GetProperty("orderNumber").GetString(),
            retryOrder.GetProperty("orderNumber").GetString());

        // I2: cùng key, body khác → 422
        var tampered = await setup.Guest.SendAsync(
            OrderRequest(setup.SessionToken, clientOrderId, setup.ProductId, key, quantity: 5));
        Assert.Equal((HttpStatusCode)422, tampered.StatusCode);

        // I3: cùng clientOrderId, key mới → trả order đã có, không tạo mới
        var newKey = await setup.Guest.SendAsync(
            OrderRequest(setup.SessionToken, clientOrderId, setup.ProductId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.OK, newKey.StatusCode);

        // Xác nhận DB chỉ có 1 order trong session
        var sessionOrders = await setup.Guest.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/table-sessions/{setup.SessionToken}/orders");
        Assert.Equal(1, sessionOrders.GetProperty("orders").GetArrayLength());
    }

    [Fact] // I5 + I6: price/tenant validation
    public async Task Validation_MonQuanKhac_VaMonHetHang()
    {
        var setupA = await SetupVenueWithMenuAndTableAsync();
        var setupB = await SetupVenueWithMenuAndTableAsync();

        // I5: món của quán B gửi vào session quán A → 400
        var crossTenant = await setupA.Guest.SendAsync(
            OrderRequest(setupA.SessionToken, Guid.NewGuid(), setupB.ProductId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.BadRequest, crossTenant.StatusCode);

        // I6: tắt món → PRODUCT_UNAVAILABLE
        await setupA.Owner.PatchAsJsonAsync($"/api/v1/products/{setupA.ProductId}/availability",
            new { isAvailable = false });
        var unavailable = await setupA.Guest.SendAsync(
            OrderRequest(setupA.SessionToken, Guid.NewGuid(), setupA.ProductId, Guid.NewGuid().ToString()));
        Assert.Equal(HttpStatusCode.Conflict, unavailable.StatusCode);
        var error = await unavailable.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PRODUCT_UNAVAILABLE", error.GetProperty("code").GetString());
    }

    [Fact] // I7–I12: transition + snapshot + outbox + history
    public async Task Transition_RowVersion_History_Snapshot()
    {
        var setup = await SetupVenueWithMenuAndTableAsync();
        var submit = await setup.Guest.SendAsync(
            OrderRequest(setup.SessionToken, Guid.NewGuid(), setup.ProductId, Guid.NewGuid().ToString()));
        var order = await submit.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();
        var rowVersion = order.GetProperty("rowVersion").GetInt64();

        // I8: rowVersion sai → 409 ORDER_STALE_VERSION
        var stale = await setup.Owner.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm",
            new { rowVersion = rowVersion + 99 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ORDER_STALE_VERSION",
            (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // Confirm đúng rowVersion
        var confirm = await setup.Owner.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm",
            new { rowVersion });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        // Nhảy cóc: confirm → complete (bỏ preparing/ready) → 409 ORDER_INVALID_STATUS
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        var skip = await setup.Owner.PostAsJsonAsync($"/api/v1/orders/{orderId}/complete",
            new { rowVersion = confirmed.GetProperty("rowVersion").GetInt64() });
        Assert.Equal(HttpStatusCode.Conflict, skip.StatusCode);
        Assert.Equal("ORDER_INVALID_STATUS",
            (await skip.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        // I9: history có 2 dòng (submitted, confirmed)
        var history = await setup.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}/history");
        Assert.Equal(2, history.GetArrayLength());

        // I7: đổi giá món sau khi đặt — order giữ snapshot cũ
        await setup.Owner.PatchAsJsonAsync($"/api/v1/products/{setup.ProductId}",
            new { basePriceMinor = 99000, rowVersion = setup.RowVersionOfProductPrice });
        var detail = await setup.Owner.GetFromJsonAsync<JsonElement>($"/api/v1/orders/{orderId}");
        Assert.Equal(20000, detail.GetProperty("items")[0].GetProperty("unitPriceMinor").GetInt64());

        // I10: user quán khác confirm → 403
        var stranger = await SetupVenueWithMenuAndTableAsync();
        var forbidden = await stranger.Owner.PostAsJsonAsync($"/api/v1/orders/{orderId}/confirm",
            new { rowVersion = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact] // I13 + I14: table session
    public async Task TableSession_DungChung_OrderThem()
    {
        var setup = await SetupVenueWithMenuAndTableAsync();

        // I13: quét lại QR → cùng session token
        var again = await (await setup.Guest.PostAsJsonAsync("/api/v1/public/table-sessions",
            new { qrToken = setup.QrToken })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(setup.SessionToken, again.GetProperty("sessionToken").GetString());

        // I14: 2 order cùng session — mã riêng, tổng cộng dồn
        await setup.Guest.SendAsync(OrderRequest(setup.SessionToken, Guid.NewGuid(), setup.ProductId,
            Guid.NewGuid().ToString()));
        await setup.Guest.SendAsync(OrderRequest(setup.SessionToken, Guid.NewGuid(), setup.ProductId,
            Guid.NewGuid().ToString(), quantity: 2));

        var list = await setup.Guest.GetFromJsonAsync<JsonElement>(
            $"/api/v1/public/table-sessions/{setup.SessionToken}/orders");
        Assert.Equal(2, list.GetProperty("orders").GetArrayLength());
        Assert.Equal(60000, list.GetProperty("totalMinor").GetInt64()); // 20k + 40k
        var number1 = list.GetProperty("orders")[0].GetProperty("orderNumber").GetString();
        var number2 = list.GetProperty("orders")[1].GetProperty("orderNumber").GetString();
        Assert.NotEqual(number1, number2);
    }
}
