using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Ordering.Domain;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Ordering.Services;

public sealed record SubmitOrderItemDto(
    Guid ProductId, Guid? VariantId, int Quantity, string? Note, List<Guid>? OptionIds);

public sealed record SubmitOrderDto(
    string SessionToken, Guid ClientOrderId, List<SubmitOrderItemDto> Items, string? CustomerNote);

public sealed record OrderItemView(
    string ProductName, string? VariantName, int Quantity, long UnitPriceMinor,
    long LineTotalMinor, string? Note, List<string> Options);

public sealed record OrderView(
    Guid Id, string OrderNumber, string Status, long SubtotalMinor, long TotalMinor,
    string CurrencyCode, string? CustomerNote, string? RejectionReason,
    DateTimeOffset PlacedAt, DateTimeOffset StatusChangedAt, long RowVersion, List<OrderItemView> Items);

public sealed class OrderService(
    TingGoDbContext db,
    ICatalogReader catalog,
    ILogger<OrderService> logger)
{
    private const string IdempotencyScope = "public-order";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// CUS-05: gửi order — idempotent theo (scope, Idempotency-Key) + unique (venue_id, client_order_id).
    /// Giá snapshot server-side; outbox ghi cùng transaction.
    /// </summary>
    public async Task<(int StatusCode, string ResponseJson)> SubmitOrderAsync(
        string idempotencyKey, SubmitOrderDto dto, CancellationToken ct)
    {
        // 1. Idempotency replay?
        var requestHash = Hash(JsonSerializer.Serialize(dto, JsonOptions));
        var existingKey = await db.Set<IdempotencyKey>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Scope == IdempotencyScope && x.Key == idempotencyKey, ct);
        if (existingKey is not null)
        {
            if (existingKey.RequestHash != requestHash)
            {
                throw new ApiException(ErrorCodes.Conflict,
                    "Idempotency-Key đã dùng cho request khác.", 422);
            }
            return (existingKey.ResponseStatus, existingKey.ResponseBody);
        }

        // 2. Validate session
        var session = await GetOpenSessionAsync(dto.SessionToken, ct);

        // 3. Client_order_id đã tồn tại? (retry không kèm key cũ)
        var existingOrder = await db.Set<Order>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.VenueId == session.VenueId && x.ClientOrderId == dto.ClientOrderId, ct);
        if (existingOrder is not null)
        {
            var replay = JsonSerializer.Serialize(await ToViewAsync(existingOrder, ct), JsonOptions);
            return (200, replay);
        }

        if (dto.Items is null || dto.Items.Count == 0)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Order phải có ít nhất một món.", 400);
        }

        // 4. Snapshot giá server-side (CUS-03: backend kiểm tra lại giá và tình trạng món)
        var productIds = dto.Items.Select(x => x.ProductId).Distinct().ToList();
        var variantIds = dto.Items.Where(x => x.VariantId is not null).Select(x => x.VariantId!.Value).Distinct().ToList();
        var optionIds = dto.Items.SelectMany(x => x.OptionIds ?? []).Distinct().ToList();

        var products = await catalog.GetProductsAsync(session.VenueId, productIds, ct);
        var variants = await catalog.GetVariantsAsync(variantIds, ct);
        var options = await catalog.GetOptionsAsync(session.VenueId, optionIds, ct);

        var order = new Order
        {
            VenueId = session.VenueId,
            TableSessionId = session.Id,
            ClientOrderId = dto.ClientOrderId,
            CustomerNote = Truncate(dto.CustomerNote, 500),
        };

        var orderItems = new List<OrderItem>();
        var orderItemModifiers = new List<OrderItemModifier>();
        long subtotal = 0;

        foreach (var item in dto.Items)
        {
            if (item.Quantity is < 1 or > 99)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Số lượng mỗi món phải từ 1 đến 99.", 400);
            }
            if (!products.TryGetValue(item.ProductId, out var product) || product.Status != "active")
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Có món không thuộc quán này.", 400);
            }
            if (!product.IsAvailable)
            {
                throw new ApiException(ErrorCodes.ProductUnavailable,
                    $"Món '{product.Name}' vừa hết hàng. Vui lòng chọn món khác.", 409);
            }

            long unitPrice = product.BasePriceMinor;
            string? variantName = null;
            if (item.VariantId is not null)
            {
                if (!variants.TryGetValue(item.VariantId.Value, out var variant)
                    || variant.ProductId != item.ProductId || !variant.IsAvailable)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Size không hợp lệ.", 400);
                }
                unitPrice += variant.PriceDeltaMinor;
                variantName = variant.Name;
            }

            var orderItem = new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                VariantNameSnapshot = variantName,
                Quantity = item.Quantity,
                Note = Truncate(item.Note, 200),
            };

            long modifierTotal = 0;
            foreach (var optionId in item.OptionIds ?? [])
            {
                if (!options.TryGetValue(optionId, out var option) || !option.IsAvailable)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "Tùy chọn không hợp lệ.", 400);
                }
                modifierTotal += option.PriceDeltaMinor;
                orderItemModifiers.Add(new OrderItemModifier
                {
                    OrderItemId = orderItem.Id,
                    ModifierOptionId = option.Id,
                    OptionNameSnapshot = option.Name,
                    UnitPriceMinor = option.PriceDeltaMinor,
                });
            }

            orderItem.UnitPriceMinor = unitPrice;
            orderItem.ModifierTotalMinor = modifierTotal;
            orderItem.LineTotalMinor = (unitPrice + modifierTotal) * item.Quantity;
            subtotal += orderItem.LineTotalMinor;
            orderItems.Add(orderItem);
        }

        order.SubtotalMinor = subtotal;
        order.TotalMinor = subtotal; // MVP: chưa discount/tax (thuế theo venue config — giai đoạn sau)
        order.CurrencyCode = products.Values.First().CurrencyCode;
        order.OrderNumber = await NextOrderNumberAsync(session.VenueId, ct);

        // 5. Transaction: order + items + history + outbox + idempotency key — tất cả hoặc không gì cả
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Add(order);
        db.AddRange(orderItems);
        db.AddRange(orderItemModifiers);
        db.Add(new OrderStatusHistory { OrderId = order.Id, FromStatus = null, ToStatus = OrderStatus.Submitted });
        db.Add(BuildOutboxEvent(order, "order.created"));

        var view = await BuildViewAsync(order, orderItems, orderItemModifiers);
        var responseJson = JsonSerializer.Serialize(view, JsonOptions);
        db.Add(new IdempotencyKey
        {
            Scope = IdempotencyScope,
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatus = 201,
            ResponseBody = responseJson,
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("duplicate key") == true)
        {
            // Race: request song song cùng key/client_order_id — unique constraint là chốt chặn cuối (I4)
            logger.LogWarning("Duplicate order submit blocked by constraint. Key={Key}", idempotencyKey);
            throw new ApiException(ErrorCodes.OrderDuplicate,
                "Order đang được xử lý. Vui lòng kiểm tra lại danh sách order.", 409);
        }

        logger.LogInformation("Order {OrderNumber} created for venue {VenueId}", order.OrderNumber, order.VenueId);
        return (201, responseJson);
    }

    public async Task<TableSession> GetOpenSessionAsync(string sessionToken, CancellationToken ct)
    {
        var hash = Hash(sessionToken);
        var session = await db.Set<TableSession>()
            .FirstOrDefaultAsync(x => x.PublicTokenHash == hash, ct)
            ?? throw new ApiException(ErrorCodes.NotFound, "Phiên bàn không tồn tại.", 404);
        if (session.Status == TableSessionStatus.Closed)
        {
            throw new ApiException(ErrorCodes.Conflict, "Phiên bàn đã đóng. Vui lòng quét lại QR.", 410);
        }
        return session;
    }

    public async Task<OrderView> ToViewAsync(Order order, CancellationToken ct)
    {
        var items = await db.Set<OrderItem>().AsNoTracking()
            .Where(x => x.OrderId == order.Id).ToListAsync(ct);
        var itemIds = items.Select(x => x.Id).ToList();
        var modifiers = await db.Set<OrderItemModifier>().AsNoTracking()
            .Where(x => itemIds.Contains(x.OrderItemId)).ToListAsync(ct);
        return await BuildViewAsync(order, items, modifiers);
    }

    private static Task<OrderView> BuildViewAsync(
        Order order, List<OrderItem> items, List<OrderItemModifier> modifiers)
        => Task.FromResult(new OrderView(
            order.Id, order.OrderNumber, order.Status, order.SubtotalMinor, order.TotalMinor,
            order.CurrencyCode, order.CustomerNote, order.RejectionReason, order.PlacedAt,
            // Thời điểm vào trạng thái hiện tại — UpdatedAt chỉ thay đổi khi chuyển trạng thái
            order.UpdatedAt, order.RowVersion,
            items.Select(i => new OrderItemView(
                i.ProductNameSnapshot, i.VariantNameSnapshot, i.Quantity, i.UnitPriceMinor,
                i.LineTotalMinor, i.Note,
                modifiers.Where(m => m.OrderItemId == i.Id).Select(m => m.OptionNameSnapshot).ToList()
            )).ToList()));

    public OutboxEvent BuildOutboxEvent(Order order, string eventType)
        => new()
        {
            VenueId = order.VenueId,
            AggregateType = "Order",
            AggregateId = order.Id,
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new
            {
                eventId = Guid.CreateVersion7(),
                eventType,
                occurredAt = DateTimeOffset.UtcNow,
                venueId = order.VenueId,
                entityId = order.Id,
                version = order.RowVersion,
                data = new
                {
                    order.Id, order.OrderNumber, order.Status, order.TotalMinor,
                    order.CurrencyCode, order.TableSessionId,
                },
            }, JsonOptions),
        };

    private async Task<string> NextOrderNumberAsync(Guid venueId, CancellationToken ct)
    {
        var todayStart = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var countToday = await db.Set<Order>()
            .CountAsync(x => x.VenueId == venueId && x.PlacedAt >= todayStart, ct);
        // Không cần unique tuyệt đối (chỉ để hiển thị) — thêm hậu tố ngẫu nhiên tránh trùng khi song song.
        return $"#{countToday + 1:D3}-{RandomNumberGenerator.GetInt32(10, 100)}";
    }

    internal static string Hash(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string? Truncate(string? value, int max)
        => value is null ? null : value.Length <= max ? value : value[..max];
}
