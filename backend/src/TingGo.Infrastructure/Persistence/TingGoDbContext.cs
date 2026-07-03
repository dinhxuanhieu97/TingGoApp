using Microsoft.EntityFrameworkCore;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Infrastructure.Persistence;

/// <summary>
/// DbContext trung tâm. Entity của từng module được đăng ký qua IModuleEntityConfigurator (DI).
/// Quy ước (CLAUDE.md): UUID PK, TIMESTAMPTZ UTC, tiền BIGINT minor, row_version, snake_case.
/// </summary>
public sealed class TingGoDbContext(
    DbContextOptions<TingGoDbContext> options,
    IEnumerable<IModuleEntityConfigurator> configurators) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var configurator in configurators)
        {
            configurator.Configure(modelBuilder);
        }
    }
}
