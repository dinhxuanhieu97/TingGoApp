using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TingGo.Infrastructure.Persistence;
using TingGo.Modules.Identity.Auth;
using TingGo.Modules.Identity.Domain;
using TingGo.Modules.Identity.Persistence;
using TingGo.Modules.Identity.Services;
using TingGo.SharedKernel.Contracts;
using TingGo.SharedKernel.Errors;
using TingGo.SharedKernel.Modules;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Identity;

public sealed record OtpRequestDto(string Email);
public sealed record OtpVerifyDto(string Email, string Code, string? DeviceName);
public sealed record RefreshDto(string RefreshToken);

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, IdentityEntityConfigurator>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<AuthService>();
        services.AddScoped<IMembershipService, MembershipService>();
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth");

        auth.MapPost("/otp/request", async (OtpRequestDto dto, AuthService service, CancellationToken ct) =>
        {
            await service.RequestOtpAsync(dto.Email, ct);
            return Results.Accepted(value: new { message = "OTP đã được gửi." });
        });

        auth.MapPost("/otp/verify", async (OtpVerifyDto dto, AuthService service, CancellationToken ct) =>
        {
            var tokens = await service.VerifyOtpAsync(dto.Email, dto.Code, dto.DeviceName, ct);
            return Results.Ok(tokens);
        });

        auth.MapPost("/refresh", async (RefreshDto dto, AuthService service, CancellationToken ct) =>
        {
            var tokens = await service.RefreshAsync(dto.RefreshToken, ct);
            return Results.Ok(tokens);
        });

        auth.MapPost("/logout", async (RefreshDto dto, AuthService service, CancellationToken ct) =>
        {
            await service.LogoutAsync(dto.RefreshToken, ct);
            return Results.NoContent();
        });

        auth.MapPost("/logout-all", async (ClaimsPrincipal principal, AuthService service, CancellationToken ct) =>
        {
            await service.LogoutAllAsync(GetUserId(principal), ct);
            return Results.NoContent();
        }).RequireAuthorization();

        endpoints.MapGet("/me", async (ClaimsPrincipal principal, TingGoDbContext db, CancellationToken ct) =>
        {
            var user = await db.Set<User>().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == GetUserId(principal), ct)
                ?? throw new ApiException(ErrorCodes.NotFound, "Không tìm thấy người dùng.", 404);
            return Results.Ok(new { user.Id, user.Email, user.DisplayName, user.Status });
        }).RequireAuthorization();

        endpoints.MapGet("/me/memberships", async (ClaimsPrincipal principal, TingGoDbContext db, CancellationToken ct) =>
        {
            var items = await db.Set<Membership>().AsNoTracking()
                .Where(x => x.UserId == GetUserId(principal) && x.Status == MembershipStatus.Active)
                .Select(x => new { x.Id, x.OrganizationId, x.VenueId, x.Role })
                .ToListAsync(ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        return endpoints;
    }

    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub")
                  ?? throw new ApiException(ErrorCodes.Unauthorized, "Token không hợp lệ.", 401);
        return Guid.Parse(sub);
    }
}
