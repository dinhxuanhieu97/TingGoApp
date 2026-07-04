namespace TingGo.Infrastructure.Persistence;

/// <summary>Quick Import — job staging (docs/prd-quick-import.md).</summary>
public sealed class ImportJob
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; set; }
    public Guid VenueId { get; set; }
    public string ImportType { get; set; } = "onboarding";
    public string Mode { get; set; } = "create_only";
    public string Status { get; set; } = ImportJobStatus.Validating;
    public string TemplateVersion { get; set; } = "2.0";
    public string OriginalFilename { get; set; } = "";
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int WarningRows { get; set; }
    public int ErrorRows { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(7);
}

public static class ImportJobStatus
{
    public const string Validating = "VALIDATING";
    public const string NeedsReview = "NEEDS_REVIEW";
    public const string ReadyToImport = "READY_TO_IMPORT";
    public const string Importing = "IMPORTING";
    public const string Completed = "COMPLETED";
    public const string CompletedWithWarnings = "COMPLETED_WITH_WARNINGS";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public sealed class ImportRow
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ImportJobId { get; set; }
    public string SectionType { get; set; } = "";
    public string SheetName { get; set; } = "";
    public int RowNumber { get; set; }
    public string SourceData { get; set; } = "{}";
    public string NormalizedData { get; set; } = "{}";
    public string RowStatus { get; set; } = "valid"; // valid | warning | error
    public string? EntityCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ImportIssue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid ImportJobId { get; set; }
    public Guid? ImportRowId { get; set; }
    public string Severity { get; set; } = "ERROR"; // ERROR | WARNING | INFO
    public string Code { get; set; } = "";
    public string? SheetName { get; set; }
    public int? RowNumber { get; set; }
    public string? FieldName { get; set; }
    public string Message { get; set; } = "";
    public string? SuggestedValue { get; set; }
}
