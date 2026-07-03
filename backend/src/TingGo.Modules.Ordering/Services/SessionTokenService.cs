using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace TingGo.Modules.Ordering.Services;

/// <summary>
/// Session token dẫn xuất từ session id bằng HMAC — server tái tạo được (trả lại khi
/// khách quét cùng bàn), DB chỉ lưu SHA256 hash (PRD 6.3: không lưu raw token).
/// </summary>
public sealed class SessionTokenService(IConfiguration configuration)
{
    public string TokenFor(Guid sessionId)
    {
        var secret = configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret chưa cấu hình.");
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var bytes = hmac.ComputeHash(sessionId.ToByteArray());
        return Convert.ToBase64String(bytes[..24]).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public string HashOf(string token) => OrderService.Hash(token);
}
