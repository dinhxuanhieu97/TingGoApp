namespace TingGo.Modules.Catalog.Services;

/// <summary>Lưu ảnh menu. Dev: local disk. Production: S3 + CloudFront (ADR-002) — thêm implementation sau.</summary>
public interface IImageStorage
{
    /// <summary>Lưu ảnh, trả về URL công khai.</summary>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default);
}
