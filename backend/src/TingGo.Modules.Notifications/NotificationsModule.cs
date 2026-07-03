using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Notifications.Domain;
using TingGo.SharedKernel.Errors;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Notifications;

public sealed record RegisterDeviceDto(string Platform, string Token, string? DeviceName);

public sealed class NotificationsModule : IModule
{
    public string Name => "Notifications";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, NotificationsEntityConfigurator>();
        services.AddSingleton<IPushSender, NoopPushSender>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/me/devices", async (
            RegisterDeviceDto dto, ClaimsPrincipal principal, TingGoDbContext db, CancellationToken ct) =>
        {
            if (dto.Platform is not ("ios" or "android") || string.IsNullOrWhiteSpace(dto.Token))
            {
                throw new ApiException(ErrorCodes.ValidationFailed,
                    "Platform phải là ios/android và token không được rỗng.", 400);
            }
            var userId = GetUserId(principal);
            var existing = await db.Set<DeviceToken>()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.Token == dto.Token, ct);
            if (existing is not null) return Results.Ok(existing);

            var device = new DeviceToken
            {
                UserId = userId,
                Platform = dto.Platform,
                Token = dto.Token,
                DeviceName = dto.DeviceName,
            };
            db.Add(device);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/me/devices/{device.Id}", device);
        }).RequireAuthorization();

        endpoints.MapGet("/me/devices", async (
            ClaimsPrincipal principal, TingGoDbContext db, CancellationToken ct) =>
        {
            var items = await db.Set<DeviceToken>().AsNoTracking()
                .Where(x => x.UserId == GetUserId(principal))
                .Select(x => new { x.Id, x.Platform, x.DeviceName, x.CreatedAt })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapDelete("/me/devices/{deviceId:guid}", async (
            Guid deviceId, ClaimsPrincipal principal, TingGoDbContext db, CancellationToken ct) =>
        {
            await db.Set<DeviceToken>()
                .Where(x => x.Id == deviceId && x.UserId == GetUserId(principal))
                .ExecuteDeleteAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        return endpoints;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
            ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return Guid.Parse(sub);
    }
}

public sealed class NotificationsEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<DeviceToken>(e =>
        {
            e.ToTable("device_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Platform).HasColumnName("platform").HasMaxLength(16);
            e.Property(x => x.Token).HasColumnName("token").HasMaxLength(512);
            e.Property(x => x.DeviceName).HasColumnName("device_name").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.UserId, x.Token }).IsUnique();
        });
    }
}

/// <summary>Log-only push sender cho tới khi cắm FCM (sprint mobile).</summary>
public sealed class NoopPushSender(ILogger<NoopPushSender> logger) : IPushSender
{
    public Task SendAsync(IReadOnlyCollection<string> deviceTokens, string title, string body,
        IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        logger.LogInformation("[Push noop] {Count} thiết bị: {Title} — {Body}",
            deviceTokens.Count, title, body);
        return Task.CompletedTask;
    }
}
