using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TingGo.Infrastructure.Persistence;

namespace TingGo.Infrastructure.Health;

public sealed class PostgresHealthCheck(TingGoDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL OK")
            : HealthCheckResult.Unhealthy("Không kết nối được PostgreSQL");
    }
}
