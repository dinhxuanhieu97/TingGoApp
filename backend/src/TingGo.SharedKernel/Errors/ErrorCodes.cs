namespace TingGo.SharedKernel.Errors;

/// <summary>
/// Error code dùng chung web/mobile. SCREAMING_SNAKE, prefix theo domain.
/// Bổ sung theo từng sprint — không đổi giá trị đã phát hành.
/// </summary>
public static class ErrorCodes
{
    // Chung
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFound = "NOT_FOUND";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string Conflict = "CONFLICT";
    public const string RateLimited = "RATE_LIMITED";
    public const string InternalError = "INTERNAL_ERROR";

    // Auth (Sprint 2)
    public const string AuthOtpExpired = "AUTH_OTP_EXPIRED";
    public const string AuthOtpInvalid = "AUTH_OTP_INVALID";
    public const string AuthTokenInvalid = "AUTH_TOKEN_INVALID";

    // Order (Sprint 5)
    public const string OrderInvalidStatus = "ORDER_INVALID_STATUS";
    public const string OrderDuplicate = "ORDER_DUPLICATE";
    public const string OrderStaleVersion = "ORDER_STALE_VERSION";

    // Menu/QR (Sprint 3–4)
    public const string MenuNotPublished = "MENU_NOT_PUBLISHED";
    public const string ProductUnavailable = "PRODUCT_UNAVAILABLE";
    public const string QrRevoked = "QR_REVOKED";
    public const string TableDisabled = "TABLE_DISABLED";
}
