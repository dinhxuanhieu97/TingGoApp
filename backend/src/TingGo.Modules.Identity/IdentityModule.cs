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
public sealed record StaffLoginDto(Guid? VenueId, string? VenueCode, string StaffCode, string Pin, string? DeviceName);
public sealed record CreateStaffDto(string DisplayName, string Role, string? StaffCode, string Pin);
public sealed record ResetPinDto(string Pin);

public sealed class IdentityModule : IModule
{
    public string Name => "Identity";

    public IServiceCollection AddModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IModuleEntityConfigurator, IdentityEntityConfigurator>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<AuthService>();
        services.AddScoped<StaffService>();
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

        auth.MapPost("/staff/login", async (
            StaffLoginDto dto, AuthService service,
            TingGo.SharedKernel.Contracts.IVenueDirectory venues, CancellationToken ct) =>
        {
            // Nhận Venue ID (cũ) hoặc mã quán 6 ký tự (mobile) — thông báo lỗi chung
            var venueId = dto.VenueId;
            if (venueId is null && !string.IsNullOrWhiteSpace(dto.VenueCode))
            {
                venueId = await venues.GetVenueIdByJoinCodeAsync(dto.VenueCode.Trim(), ct);
            }
            if (venueId is null)
            {
                throw new ApiException(ErrorCodes.AuthStaffLoginInvalid,
                    "Mã quán, mã nhân viên hoặc PIN không đúng.", 401);
            }
            var tokens = await service.StaffLoginAsync(venueId.Value, dto.StaffCode, dto.Pin, dto.DeviceName, ct);
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

        // MER-07 (phần Sprint 2): tạo và xem nhân viên để staff login dùng được.
        endpoints.MapPost("/venues/{venueId:guid}/staff", async (
            Guid venueId, CreateStaffDto dto, ClaimsPrincipal principal,
            StaffService service, CancellationToken ct) =>
        {
            var created = await service.CreateStaffAsync(
                GetUserId(principal), venueId, dto.DisplayName, dto.Role, dto.StaffCode, dto.Pin, ct);
            return Results.Created($"/api/v1/venues/{venueId}/staff/{created.MembershipId}", created);
        }).RequireAuthorization();

        endpoints.MapGet("/venues/{venueId:guid}/staff", async (
            Guid venueId, ClaimsPrincipal principal, StaffService service, CancellationToken ct) =>
        {
            var items = await service.ListStaffAsync(GetUserId(principal), venueId, ct);
            return Results.Ok(items);
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/staff/{staffId:guid}/reset-pin", async (
            Guid venueId, Guid staffId, ResetPinDto dto, ClaimsPrincipal principal,
            StaffService service, CancellationToken ct) =>
        {
            await service.ResetPinAsync(GetUserId(principal), venueId, staffId, dto.Pin, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/staff/{staffId:guid}/revoke", async (
            Guid venueId, Guid staffId, ClaimsPrincipal principal,
            StaffService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(GetUserId(principal), venueId, staffId, active: false, ct);
            return Results.NoContent();
        }).RequireAuthorization();

        endpoints.MapPost("/venues/{venueId:guid}/staff/{staffId:guid}/activate", async (
            Guid venueId, Guid staffId, ClaimsPrincipal principal,
            StaffService service, CancellationToken ct) =>
        {
            await service.SetStatusAsync(GetUserId(principal), venueId, staffId, active: true, ct);
            return Results.NoContent();
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
