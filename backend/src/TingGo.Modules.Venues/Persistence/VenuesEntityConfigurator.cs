using Microsoft.EntityFrameworkCore;
using TingGo.Modules.Venues.Domain;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Venues.Persistence;

public sealed class VenuesEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<Organization>(e =>
        {
            e.ToTable("organizations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.DefaultLocale).HasColumnName("default_locale").HasMaxLength(10);
            e.Property(x => x.DefaultCurrency).HasColumnName("default_currency").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        b.Entity<Venue>(e =>
        {
            e.ToTable("venues");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrganizationId).HasColumnName("organization_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(100);
            e.Property(x => x.CountryCode).HasColumnName("country_code").HasMaxLength(2).IsFixedLength();
            e.Property(x => x.Timezone).HasColumnName("timezone").HasMaxLength(64);
            e.Property(x => x.DefaultLocale).HasColumnName("default_locale").HasMaxLength(10);
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.WifiName).HasColumnName("wifi_name").HasMaxLength(200);
            e.Property(x => x.WifiPasswordEncrypted).HasColumnName("wifi_password_encrypted");
            e.Property(x => x.BankQrImageUrl).HasColumnName("bank_qr_image_url");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.OrganizationId);
        });

        b.Entity<VenueArea>(e =>
        {
            e.ToTable("venue_areas");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasIndex(x => x.VenueId);
        });

        b.Entity<DiningTable>(e =>
        {
            e.ToTable("dining_tables");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.AreaId).HasColumnName("area_id");
            e.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.VenueId, x.Code }).IsUnique();
            e.HasIndex(x => x.AreaId);
        });

        b.Entity<OpeningHour>(e =>
        {
            e.ToTable("opening_hours");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.DayOfWeekIso).HasColumnName("day_of_week");
            e.Property(x => x.OpenTime).HasColumnName("open_time");
            e.Property(x => x.CloseTime).HasColumnName("close_time");
            e.Property(x => x.IsClosed).HasColumnName("is_closed");
            e.HasIndex(x => new { x.VenueId, x.DayOfWeekIso }).IsUnique();
        });

        b.Entity<QrCode>(e =>
        {
            e.ToTable("qr_codes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.TableId).HasColumnName("table_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.TableId);
        });
    }
}
