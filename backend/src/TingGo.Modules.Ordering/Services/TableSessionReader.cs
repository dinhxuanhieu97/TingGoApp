using Microsoft.EntityFrameworkCore;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Ordering.Domain;
using TingGo.SharedKernel.Contracts;

namespace TingGo.Modules.Ordering.Services;

public sealed class TableSessionReader(TingGoDbContext db) : ITableSessionReader
{
    private static readonly string[] BillableStatuses =
        [OrderStatus.Submitted, OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed];

    public async Task<SessionBill?> GetSessionBillAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.Set<TableSession>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return null;

        var orders = await db.Set<Order>().AsNoTracking()
            .Where(o => o.TableSessionId == sessionId && BillableStatuses.Contains(o.Status))
            .Select(o => new { o.TotalMinor, o.CurrencyCode })
            .ToListAsync(ct);

        return new SessionBill(session.Id, session.VenueId, session.TableId, session.Status,
            orders.Sum(o => o.TotalMinor), orders.FirstOrDefault()?.CurrencyCode ?? "VND");
    }
}
