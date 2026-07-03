namespace TingGo.Modules.Ordering.Domain;

public sealed class Order
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid TableSessionId { get; set; }
    public string OrderNumber { get; set; } = "";
    public Guid ClientOrderId { get; set; }
    public string Status { get; set; } = OrderStatus.Submitted;
    public long SubtotalMinor { get; set; }
    public long DiscountMinor { get; set; }
    public long TaxMinor { get; set; }
    public long TotalMinor { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public string? CustomerNote { get; set; }
    public string? RejectionReason { get; set; }
    public DateTimeOffset? EstimatedReadyAt { get; set; }
    public DateTimeOffset PlacedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class OrderStatus
{
    public const string Submitted = "submitted";
    public const string Confirmed = "confirmed";
    public const string Preparing = "preparing";
    public const string Ready = "ready";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

/// <summary>Snapshot tên/giá tại thời điểm đặt (PRD 6.3) — không chỉ FK.</summary>
public sealed class OrderItem
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public Guid? ProductId { get; set; }
    public string ProductNameSnapshot { get; set; } = "";
    public string? VariantNameSnapshot { get; set; }
    public int Quantity { get; set; }
    public long UnitPriceMinor { get; set; }
    public long ModifierTotalMinor { get; set; }
    public long LineTotalMinor { get; set; }
    public string? Note { get; set; }
}

public sealed class OrderItemModifier
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderItemId { get; set; }
    public Guid? ModifierOptionId { get; set; }
    public string OptionNameSnapshot { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public long UnitPriceMinor { get; set; }
}

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = "";
    public Guid? ActorMembershipId { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
