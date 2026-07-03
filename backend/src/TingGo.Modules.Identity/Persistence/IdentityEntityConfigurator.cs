using Microsoft.EntityFrameworkCore;
using TingGo.Modules.Identity.Domain;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Identity.Persistence;

public sealed class IdentityEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(x => x.PhoneE164).HasColumnName("phone_e164").HasMaxLength(32);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.PhoneE164).IsUnique();
        });

        b.Entity<OtpCode>(e =>
        {
            e.ToTable("otp_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").HasMaxLength(320);
            e.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(128);
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.Email, x.CreatedAt });
        });

        b.Entity<UserSession>(e =>
        {
            e.ToTable("user_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(128);
            e.Property(x => x.DeviceName).HasColumnName("device_name").HasMaxLength(200);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            e.Property(x => x.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Ignore(x => x.IsActive);
            e.HasIndex(x => x.RefreshTokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
        });

        b.Entity<Membership>(e =>
        {
            e.ToTable("memberships");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.Role).HasColumnName("role").HasMaxLength(32);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.StaffCode).HasColumnName("staff_code").HasMaxLength(32);
            e.Property(x => x.PinHash).HasColumnName("pin_hash");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.UserId, x.VenueId });
            e.HasIndex(x => new { x.VenueId, x.Status });
        });
    }
}
