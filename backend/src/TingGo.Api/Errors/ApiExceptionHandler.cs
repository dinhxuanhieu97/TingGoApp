using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Errors;

/// <summary>
/// Global exception handler — mọi lỗi trả về đúng format PRD 7.1:
/// { code, message, traceId, details }.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        var (statusCode, error) = exception switch
        {
            ApiException apiEx => (apiEx.StatusCode,
                new ApiError(apiEx.Code, apiEx.Message, traceId, apiEx.Details)),
            _ => (StatusCodes.Status500InternalServerError,
                new ApiError(ErrorCodes.InternalError, "Đã xảy ra lỗi hệ thống.", traceId)),
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        }
        else
        {
            logger.LogWarning("Business error {Code}. TraceId={TraceId}", error.Code, traceId);
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(error, cancellationToken);
        return true;
    }
}
