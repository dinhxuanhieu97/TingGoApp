using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TingGo.Modules.Catalog.Services;
using TingGo.SharedKernel.Errors;

namespace TingGo.Modules.Catalog.Endpoints;

public static class FileEndpoints
{
    private const long MaxSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/files/images", async (IFormFile file, IImageStorage storage, CancellationToken ct) =>
        {
            if (file.Length is 0 or > MaxSizeBytes)
            {
                throw new ApiException(ErrorCodes.ValidationFailed, "Ảnh phải nhỏ hơn 5 MB.", 400);
            }
            if (!LocalImageStorage.IsAllowed(file.ContentType))
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Chỉ chấp nhận ảnh JPEG, PNG hoặc WebP.", 400);
            }

            await using var stream = file.OpenReadStream();
            var url = await storage.SaveAsync(stream, file.ContentType, ct);
            return Results.Created(url, new { url });
        })
        .RequireAuthorization()
        .DisableAntiforgery();
    }
}
