using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TingGo.Api.Hubs;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Identity.Domain;
using TingGo.Modules.Notifications.Domain;
using TingGo.Modules.Ordering.Services;

namespace TingGo.Api.Workers;

/// <summary>
/// Đọc outbox_events pending và phát qua SignalR (PRD Sprint 6).
/// Order đã commit DB không phụ thuộc notification thành công (NFR 5.2) —
/// lỗi phát chỉ retry outbox, không ảnh hưởng order.
/// </summary>
public sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<OrderHub> hub,
    SessionTokenService sessionTokens,
    IPushSender pushSender,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 10;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMilliseconds(300);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxWorker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await ProcessBatchAsync(stoppingToken);
                if (processed == 0)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "OutboxWorker batch error");
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<int> ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TingGoDbContext>();

        var events = await db.Set<OutboxEvent>()
            .Where(x => x.Status == OutboxStatus.Pending && x.NextAttemptAt <= DateTimeOffset.UtcNow)
            .OrderBy(x => x.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var outboxEvent in events)
        {
            try
            {
                await PublishAsync(outboxEvent, ct);
                await PushIfNeededAsync(db, outboxEvent, ct);
                outboxEvent.Status = OutboxStatus.Completed;
                outboxEvent.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                outboxEvent.Attempts++;
                outboxEvent.NextAttemptAt = DateTimeOffset.UtcNow
                    .AddSeconds(Math.Min(60, Math.Pow(2, outboxEvent.Attempts)));
                if (outboxEvent.Attempts >= MaxAttempts)
                {
                    outboxEvent.Status = OutboxStatus.Completed; // bỏ qua sau max retry — log để điều tra
                    logger.LogError(ex, "Outbox event {Id} ({Type}) bỏ qua sau {Attempts} lần",
                        outboxEvent.Id, outboxEvent.EventType, outboxEvent.Attempts);
                }
                else
                {
                    logger.LogWarning(ex, "Outbox event {Id} lỗi, retry lần {Attempts}",
                        outboxEvent.Id, outboxEvent.Attempts);
                }
            }
        }

        if (events.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
        return events.Count;
    }

    /// <summary>MOB-02: push khi có đơn mới / khách gọi nhân viên — gửi tới thiết bị của staff quán.</summary>
    private async Task PushIfNeededAsync(TingGoDbContext db, OutboxEvent outboxEvent, CancellationToken ct)
    {
        var (title, body) = outboxEvent.EventType switch
        {
            "order.created" => ("TingGo — Đơn mới 🔔", "Có đơn hàng mới, mở app để xác nhận."),
            "service_request.created" => ("TingGo — Khách gọi 🙋", "Khách đang cần hỗ trợ tại bàn."),
            _ => (null, null),
        };
        if (title is null) return;

        var organizationId = await db.Set<TingGo.Modules.Venues.Domain.Venue>().AsNoTracking()
            .Where(v => v.Id == outboxEvent.VenueId)
            .Select(v => (Guid?)v.OrganizationId)
            .FirstOrDefaultAsync(ct);
        if (organizationId is null) return;

        var tokens = await (
            from membership in db.Set<Membership>().AsNoTracking()
            join device in db.Set<DeviceToken>().AsNoTracking() on membership.UserId equals device.UserId
            where membership.Status == MembershipStatus.Active
                  && (membership.VenueId == outboxEvent.VenueId
                      || (membership.VenueId == null && membership.OrganizationId == organizationId))
            select device.Token
        ).Distinct().Take(100).ToListAsync(ct);
        if (tokens.Count > 0)
        {
            await pushSender.SendAsync(tokens, title, body!,
                new Dictionary<string, string> { ["venueId"] = outboxEvent.VenueId.ToString() }, ct);
        }
    }

    private async Task PublishAsync(OutboxEvent outboxEvent, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(outboxEvent.Payload);
        var payload = doc.RootElement.Clone();

        // Merchant: group theo venue
        await hub.Clients.Group($"venue:{outboxEvent.VenueId}")
            .SendAsync(outboxEvent.EventType, payload, ct);

        // Khách: group theo table session (token dẫn xuất từ sessionId)
        if (payload.TryGetProperty("data", out var data)
            && data.TryGetProperty("tableSessionId", out var sessionIdProp)
            && sessionIdProp.TryGetGuid(out var sessionId))
        {
            var sessionToken = sessionTokens.TokenFor(sessionId);
            await hub.Clients.Group($"session:{sessionToken}")
                .SendAsync(outboxEvent.EventType, payload, ct);
        }
    }
}
