using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace TingGo.Modules.Identity.Auth;

/// <summary>
/// SMTP sender — dev trỏ Mailpit (localhost:1025). Production: SES (ADR-003).
/// SmtpClient đủ cho MVP; đổi sang MailKit nếu cần OAuth/TLS nâng cao.
/// </summary>
public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
    {
        var host = configuration["Smtp:Host"] ?? "localhost";
        var port = configuration.GetValue("Smtp:Port", 1025);
        var from = configuration["Smtp:From"] ?? "no-reply@tinggo.local";

        using var client = new SmtpClient(host, port);
        using var message = new MailMessage(from, to, subject, body);
        await client.SendMailAsync(message, ct);
    }
}
