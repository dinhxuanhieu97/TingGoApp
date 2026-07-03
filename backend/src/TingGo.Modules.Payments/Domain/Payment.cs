namespace TingGo.Modules.Payments.Domain;

/// <summary>ADR-004: MVP chỉ cash + bank_transfer (QR tĩnh), xác nhận thủ công. payOS ở Commercial V1.</summary>
public sealed class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid VenueId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? TableSessionId { get; set; }
    public string Provider { get; set; } = PaymentProvider.Cash;
    public string? ProviderPaymentId { get; set; }
    public string Method { get; set; } = PaymentMethod.Cash;
    public string Status { get; set; } = PaymentStatus.Pending;
    public long AmountMinor { get; set; }
    public string CurrencyCode { get; set; } = "VND";
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public static class PaymentProvider
{
    public const string Cash = "cash";
}

public static class PaymentMethod
{
    public const string Cash = "cash";
    public const string BankTransfer = "bank_transfer";
    public static readonly string[] All = [Cash, BankTransfer];
}

public static class PaymentStatus
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Cancelled = "cancelled";
    public const string Failed = "failed";
}
