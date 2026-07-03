namespace TingGo.SharedKernel.Errors;

/// <summary>Exception nghiệp vụ — được middleware chuyển thành ApiError.</summary>
public sealed class ApiException(
    string code,
    string message,
    int statusCode = 400,
    IReadOnlyDictionary<string, object?>? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
    public IReadOnlyDictionary<string, object?>? Details { get; } = details;
}
