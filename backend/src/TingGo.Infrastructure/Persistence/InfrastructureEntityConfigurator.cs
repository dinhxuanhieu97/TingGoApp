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

        b.Entity<ImportJob>(e =>
        {
            e.ToTable("import_jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.ImportType).HasColumnName("import_type").HasMaxLength(32);
            e.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(32);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.TemplateVersion).HasColumnName("template_version").HasMaxLength(20);
            e.Property(x => x.OriginalFilename).HasColumnName("original_filename").HasMaxLength(255);
            e.Property(x => x.TotalRows).HasColumnName("total_rows");
            e.Property(x => x.ValidRows).HasColumnName("valid_rows");
            e.Property(x => x.WarningRows).HasColumnName("warning_rows");
            e.Property(x => x.ErrorRows).HasColumnName("error_rows");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.HasIndex(x => new { x.VenueId, x.CreatedAt });
        });

        b.Entity<ImportRow>(e =>
        {
            e.ToTable("import_rows");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ImportJobId).HasColumnName("import_job_id");
            e.Property(x => x.SectionType).HasColumnName("section_type").HasMaxLength(50);
            e.Property(x => x.SheetName).HasColumnName("sheet_name").HasMaxLength(100);
            e.Property(x => x.RowNumber).HasColumnName("row_number");
            e.Property(x => x.SourceData).HasColumnName("source_data").HasColumnType("jsonb");
            e.Property(x => x.NormalizedData).HasColumnName("normalized_data").HasColumnType("jsonb");
            e.Property(x => x.RowStatus).HasColumnName("row_status").HasMaxLength(32);
            e.Property(x => x.EntityCode).HasColumnName("entity_code").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.ImportJobId, x.SectionType });
        });

        b.Entity<ImportIssue>(e =>
        {
            e.ToTable("import_issues");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ImportJobId).HasColumnName("import_job_id");
            e.Property(x => x.ImportRowId).HasColumnName("import_row_id");
            e.Property(x => x.Severity).HasColumnName("severity").HasMaxLength(20);
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(100);
            e.Property(x => x.SheetName).HasColumnName("sheet_name").HasMaxLength(100);
            e.Property(x => x.RowNumber).HasColumnName("row_number");
            e.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(100);
            e.Property(x => x.Message).HasColumnName("message");
            e.Property(x => x.SuggestedValue).HasColumnName("suggested_value");
            e.HasIndex(x => x.ImportJobId);
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
