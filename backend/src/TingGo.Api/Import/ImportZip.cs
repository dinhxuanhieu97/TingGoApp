using System.IO.Compression;
using TingGo.Infrastructure.Persistence;
using TingGo.SharedKernel.Errors;

namespace TingGo.Api.Import;

public sealed record StagedAsset(string FileName, string StagingPath, string ContentType, long SizeBytes);

/// <summary>
/// Giải nén an toàn gói tinggo-import.zip (PRD 3.2 + 11.1):
/// chặn path traversal, zip lồng nhau, file thực thi; ảnh validate bằng magic bytes.
/// </summary>
public static class ImportZip
{
    private const long MaxZipBytes = 200 * 1024 * 1024;
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const int MaxImages = 1000;

    public static bool IsZip(string fileName, Stream stream)
    {
        if (!fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return false;
        Span<byte> header = stackalloc byte[4];
        var read = stream.Read(header);
        stream.Position = 0;
        return read == 4 && header[0] == 0x50 && header[1] == 0x4B; // "PK"
    }

    /// <summary>Trả về stream file xlsx + danh sách ảnh đã staging + issues (jobId gắn sau).</summary>
    public static (MemoryStream Xlsx, List<StagedAsset> Assets, List<ImportIssue> Issues) Extract(
        Stream zipStream, long zipLength, Guid jobId, string stagingRoot)
    {
        if (zipLength > MaxZipBytes)
        {
            throw new ApiException(ErrorCodes.ValidationFailed, "Gói ZIP phải nhỏ hơn 200 MB.", 400);
        }

        var issues = new List<ImportIssue>();
        var assets = new List<StagedAsset>();
        MemoryStream? xlsx = null;
        var stagingDir = Path.Combine(stagingRoot, jobId.ToString("N"));
        Directory.CreateDirectory(stagingDir);

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue; // thư mục

            // Path traversal / đường dẫn tuyệt đối (PRD 11.1)
            if (entry.FullName.Contains("..") || Path.IsPathRooted(entry.FullName))
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    $"ZIP chứa đường dẫn không hợp lệ: {entry.FullName}", 400);
            }

            var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
            if (extension == ".zip")
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Không chấp nhận file nén lồng nhau.", 400);
            }
            if (extension is ".exe" or ".dll" or ".sh" or ".bat" or ".cmd" or ".js" or ".vbs")
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    $"ZIP chứa file không được phép: {entry.Name}", 400);
            }

            if (extension == ".xlsx")
            {
                if (xlsx is not null)
                {
                    throw new ApiException(ErrorCodes.ValidationFailed, "ZIP chỉ được chứa một file .xlsx.", 400);
                }
                xlsx = new MemoryStream();
                using var entryStream = entry.Open();
                entryStream.CopyTo(xlsx);
                xlsx.Position = 0;
                continue;
            }

            if (extension is ".jpg" or ".jpeg" or ".png" or ".webp")
            {
                var fileName = Path.GetFileName(entry.Name);
                if (assets.Count >= MaxImages)
                {
                    issues.Add(Issue(jobId, "WARNING", "IMPORT_TOO_MANY_IMAGES",
                        $"Vượt {MaxImages} ảnh — '{fileName}' bị bỏ qua."));
                    continue;
                }
                if (entry.Length > MaxImageBytes)
                {
                    issues.Add(Issue(jobId, "WARNING", "IMPORT_IMAGE_TOO_LARGE",
                        $"Ảnh '{fileName}' vượt 5 MB — bỏ qua."));
                    continue;
                }

                using var entryStream = entry.Open();
                var buffer = new MemoryStream();
                entryStream.CopyTo(buffer);
                var bytes = buffer.ToArray();
                var contentType = SniffImage(bytes);
                if (contentType is null)
                {
                    issues.Add(Issue(jobId, "WARNING", "IMPORT_IMAGE_INVALID",
                        $"'{fileName}' không phải ảnh JPG/PNG/WebP hợp lệ — bỏ qua."));
                    continue;
                }

                // Không dùng tên file người dùng làm storage key (PRD 11.2)
                var stagingPath = Path.Combine(stagingDir, $"{Guid.CreateVersion7():N}{extension}");
                File.WriteAllBytes(stagingPath, bytes);
                assets.Add(new StagedAsset(fileName, stagingPath, contentType, bytes.Length));
            }
            // File khác (README.txt...) — bỏ qua im lặng
        }

        if (xlsx is null)
        {
            CleanupStaging(stagingRoot, jobId);
            throw new ApiException(ErrorCodes.ValidationFailed, "ZIP phải chứa một file Excel .xlsx.", 400);
        }
        return (xlsx, assets, issues);
    }

    public static void CleanupStaging(string stagingRoot, Guid jobId)
    {
        var dir = Path.Combine(stagingRoot, jobId.ToString("N"));
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* dọn dẹp best-effort */ }
    }

    /// <summary>Magic bytes: JPG FFD8FF, PNG 89504E47, WebP RIFF....WEBP.</summary>
    private static string? SniffImage(byte[] bytes)
    {
        if (bytes.Length < 12) return null;
        if (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "image/jpeg";
        if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "image/png";
        if (bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50) return "image/webp";
        return null;
    }

    private static ImportIssue Issue(Guid jobId, string severity, string code, string message)
        => new() { ImportJobId = jobId, Severity = severity, Code = code, SheetName = "images/", Message = message };
}
