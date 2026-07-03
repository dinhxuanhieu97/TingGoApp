namespace TingGo.SharedKernel.Errors;

/// <summary>Response lỗi thống nhất theo PRD mục 7.1.</summary>
public sealed record ApiError(
    string Code,
    string Message,
    string TraceId,
    IReadOnlyDictionary<string, object?>? Details = null);
