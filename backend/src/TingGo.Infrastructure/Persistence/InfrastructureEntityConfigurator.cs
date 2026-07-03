using Microsoft.EntityFrameworkCore;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Infrastructure.Persistence;

public sealed class InfrastructureEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<OutboxEvent>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100);
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200);
            e.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            e.HasIndex(x => new { x.Status, x.NextAttemptAt });
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            e.Property(x => x.Action).HasColumnName("action").HasMaxLength(100);
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100);
            e.Property(x => x.EntityId).HasColumnName("entity_id");
            e.Property(x => x.Detail).HasColumnName("detail");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.VenueId, x.CreatedAt });
        });

        b.Entity<IdempotencyKey>(e =>
        {
            e.ToTable("idempotency_keys");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(100);
            e.Property(x => x.Key).HasColumnName("key").HasMaxLength(200);
            e.Property(x => x.RequestHash).HasColumnName("request_hash").HasMaxLength(128);
            e.Property(x => x.ResponseStatus).HasColumnName("response_status");
            e.Property(x => x.ResponseBody).HasColumnName("response_body").HasColumnType("jsonb");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.Scope, x.Key }).IsUnique();
        });
    }
}
