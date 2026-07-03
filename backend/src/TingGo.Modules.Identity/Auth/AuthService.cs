using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Identity.Domain;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Identity.Auth;

public sealed record AuthTokens(string AccessToken, string RefreshToken, Guid UserId, string? Email);

public sealed class AuthService(
    TingGoDbContext db,
    JwtTokenService jwt,
    IEmailSender emailSender,
    ILogger<AuthService> logger)
{
    private const int MaxOtpPerHour = 5;
    private const int MaxVerifyAttempts = 5;
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

    public async Task RequestOtpAsync(string email, CancellationToken ct)
    {
        email = NormalizeEmail(email);

        var oneHourAgo = DateTimeOffset.UtcNow.AddHours(-1);
        var recentCount = await db.Set<OtpCode>()
            .CountAsync(x => x.Email == email && x.CreatedAt > oneHourAgo, ct);
        if (recentCount >= MaxOtpPerHour)
        {
            throw new ApiException(ErrorCodes.RateLimited,
                "Bạn đã yêu cầu OTP quá nhiều lần. Vui lòng thử lại sau.", 429);
        }

        var code = TokenHashing.GenerateOtpCode();
        db.Add(new OtpCode
        {
            Email = email,
            CodeHash = TokenHashing.Sha256(code),
            ExpiresAt = DateTimeOffset.UtcNow.Add(OtpLifetime),
        });
        await db.SaveChangesAsync(ct);

        await emailSender.SendAsync(email,
            "TingGo — mã đăng nhập của bạn",
            $"Mã OTP của bạn là: {code}\nMã có hiệu lực trong 5 phút.", ct);

        logger.LogInformation("OTP sent to {Email}", email);
    }

    public async Task<AuthTokens> VerifyOtpAsync(string email, string code, string? deviceName, CancellationToken ct)
    {
        email = NormalizeEmail(email);

        var otp = await db.Set<OtpCode>()
            .Where(x => x.Email == email && x.ConsumedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ApiException(ErrorCodes.AuthOtpExpired, "Mã OTP không tồn tại hoặc đã hết hạn.", 400);

        if (otp.Attempts >= MaxVerifyAttempts)
        {
            throw new ApiException(ErrorCodes.AuthOtpInvalid, "Nhập sai quá nhiều lần. Hãy yêu cầu mã mới.", 400);
        }

        otp.Attempts++;
        if (otp.CodeHash != TokenHashing.Sha256(code))
        {
            await db.SaveChangesAsync(ct);
            throw new ApiException(ErrorCodes.AuthOtpInvalid, "Mã OTP không đúng.", 400);
        }

        otp.ConsumedAt = DateTimeOffset.UtcNow;

        var user = await db.Set<User>().FirstOrDefaultAsync(x => x.Email == email, ct);
        if (user is null)
        {
            user = new User { Email = email, DisplayName = email.Split('@')[0] };
            db.Add(user);
        }
        else if (user.Status == UserStatus.Blocked)
        {
            throw new ApiException(ErrorCodes.Forbidden, "Tài khoản đã bị khóa.", 403);
        }

        var tokens = CreateSession(user, deviceName);
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task<AuthTokens> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenHashing.Sha256(refreshToken);
        var session = await db.Set<UserSession>()
            .FirstOrDefaultAsync(x => x.RefreshTokenHash == hash, ct)
            ?? throw new ApiException(ErrorCodes.AuthTokenInvalid, "Refresh token không hợp lệ.", 401);

        if (session.RevokedAt is not null)
        {
            // Token reuse — thu hồi toàn bộ session của user (bảo mật rotation).
            logger.LogWarning("Refresh token reuse detected for user {UserId}", session.UserId);
            await db.Set<UserSession>()
                .Where(x => x.UserId == session.UserId && x.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
            throw new ApiException(ErrorCodes.AuthTokenInvalid, "Phiên đăng nhập không hợp lệ.", 401);
        }

        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ApiException(ErrorCodes.AuthTokenInvalid, "Phiên đăng nhập đã hết hạn.", 401);
        }

        var user = await db.Set<User>().FirstAsync(x => x.Id == session.UserId, ct);
        var tokens = CreateSession(user, session.DeviceName);
        session.RevokedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return tokens;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenHashing.Sha256(refreshToken);
        await db.Set<UserSession>()
            .Where(x => x.RefreshTokenHash == hash && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    public async Task LogoutAllAsync(Guid userId, CancellationToken ct)
    {
        await db.Set<UserSession>()
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, DateTimeOffset.UtcNow), ct);
    }

    private AuthTokens CreateSession(User user, string? deviceName)
    {
        var refreshToken = TokenHashing.GenerateRefreshToken();
        db.Add(new UserSession
        {
            UserId = user.Id,
            RefreshTokenHash = TokenHashing.Sha256(refreshToken),
            DeviceName = deviceName,
            ExpiresAt = DateTimeOffset.UtcNow.Add(RefreshLifetime),
        });
        return new AuthTokens(jwt.CreateAccessToken(user), refreshToken, user.Id, user.Email);
    }

    private static string NormalizeEmail(string email)
    {
        email = email.Trim().ToLowerInvariant();
        if (email.Length is < 3 or > 320 || !email.Contains('@'))
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Email không hợp lệ.", 400);
        }
        return email;
    }
}
