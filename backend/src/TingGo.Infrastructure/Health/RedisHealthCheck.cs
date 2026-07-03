using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace TingGo.Infrastructure.Health;

public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var latency = await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis OK ({latency.TotalMilliseconds:F0} ms)");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Không kết nối được Redis", ex);
        }
    }
}
