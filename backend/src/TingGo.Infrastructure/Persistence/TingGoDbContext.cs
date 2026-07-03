using Microsoft.EntityFrameworkCore;

namespace TingGo.Infrastructure.Persistence;

/// <summary>
/// DbContext trung tâm. Entity configuration của từng module được nạp qua
/// IEntityTypeConfiguration trong assembly module (đăng ký từ Sprint 2).
/// Quy ước (CLAUDE.md): UUID PK, TIMESTAMPTZ UTC, tiền BIGINT minor, row_version.
/// </summary>
public sealed class TingGoDbContext(DbContextOptions<TingGoDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Sprint 2+: modelBuilder.ApplyConfigurationsFromAssembly(...) cho từng module.
    }
}
