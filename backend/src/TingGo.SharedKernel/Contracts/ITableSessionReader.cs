namespace TingGo.SharedKernel.Contracts;

public sealed record SessionBill(Guid SessionId, Guid VenueId, Guid TableId, string Status, long TotalMinor, string CurrencyCode);

/// <summary>Contract cross-module (impl tại Ordering) — Payments đọc bill của phiên bàn.</summary>
public interface ITableSessionReader
{
    /// <summary>Bill của phiên: tổng các order không bị reject/cancel. Null nếu phiên không tồn tại.</summary>
    Task<SessionBill?> GetSessionBillAsync(Guid sessionId, CancellationToken ct = default);
}
