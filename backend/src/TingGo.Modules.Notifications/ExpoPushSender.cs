using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TingGo.Modules.Notifications.Domain;

namespace TingGo.Modules.Notifications;

/// <summary>
/// Gửi push qua Expo Push API (https://exp.host) — không cần credentials, hoạt động với Expo Go.
/// Token dạng "ExponentPushToken[...]" do app mobile đăng ký. FCM trực tiếp: chuyển ở bản release EAS.
/// </summary>
public sealed class ExpoPushSender(IHttpClientFactory httpClientFactory, ILogger<ExpoPushSender> logger)
    : IPushSender
{
    public async Task SendAsync(IReadOnlyCollection<string> deviceTokens, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        var expoTokens = deviceTokens
            .Where(t => t.StartsWith("ExponentPushToken", StringComparison.Ordinal))
            .ToList();
        if (expoTokens.Count == 0) return;

        var client = httpClientFactory.CreateClient("expo-push");
        var messages = expoTokens.Select(token => new
        {
            to = token,
            title,
            body,
            sound = "default",
            data,
        });
        try
        {
            var response = await client.PostAsJsonAsync("https://exp.host/--/api/v2/push/send", messages, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Expo push trả {Status}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Push lỗi không được ảnh hưởng nghiệp vụ (NFR 5.2)
            logger.LogWarning(ex, "Expo push gửi lỗi — bỏ qua");
        }
    }
}
