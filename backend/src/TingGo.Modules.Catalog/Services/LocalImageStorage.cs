using Microsoft.Extensions.Configuration;

namespace TingGo.Modules.Catalog.Services;

public sealed class LocalImageStorage(IConfiguration configuration) : IImageStorage
{
    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var extension = AllowedTypes[contentType];
        var directory = configuration["ImageStorage:LocalPath"] ?? "uploads/images";
        Directory.CreateDirectory(directory);

        var fileName = $"{Guid.CreateVersion7():N}{extension}";
        var filePath = Path.Combine(directory, fileName);
        await using var file = File.Create(filePath);
        await content.CopyToAsync(file, ct);

        return $"/files/images/{fileName}";
    }

    public static bool IsAllowed(string contentType) => AllowedTypes.ContainsKey(contentType);
}
