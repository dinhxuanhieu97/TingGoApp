namespace TingGo.Modules.Venues.Domain;

public sealed class Venue
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string CountryCode { get; set; } = "VN";
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string DefaultLocale { get; set; } = "vi-VN";
    public string CurrencyCode { get; set; } = "VND";
    public string Status { get; set; } = "active";
    public string? WifiName { get; set; }
    public string? WifiPasswordEncrypted { get; set; }
    /// <summary>ADR-004: ảnh QR ngân hàng tĩnh (VietQR) hiển thị cho khách khi thanh toán.</summary>
    public string? BankQrImageUrl { get; set; }
    /// <summary>Mã quán 6 ký tự cho nhân viên đăng nhập mobile (thay Venue ID dài).</summary>
    public string JoinCode { get; set; } = GenerateJoinCode();

    public static string GenerateJoinCode()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // bỏ ký tự dễ nhầm
        return string.Create(6, alphabet, (span, chars) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = chars[System.Security.Cryptography.RandomNumberGenerator.GetInt32(chars.Length)];
            }
        });
    }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
