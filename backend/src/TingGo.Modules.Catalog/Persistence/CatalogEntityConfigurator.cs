using Microsoft.EntityFrameworkCore;
using TingGo.Modules.Catalog.Domain;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Catalog.Persistence;

public sealed class CatalogEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<Menu>(e =>
        {
            e.ToTable("menus");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.VenueId);
        });

        b.Entity<MenuCategory>(e =>
        {
            e.ToTable("menu_categories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.MenuId).HasColumnName("menu_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.IsVisible).HasColumnName("is_visible");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.MenuId);
        });

        b.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.BasePriceMinor).HasColumnName("base_price_minor");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.ImageUrl).HasColumnName("image_url");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.IsAvailable).HasColumnName("is_available");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.VenueId, x.CategoryId, x.Status });
            e.HasIndex(x => new { x.VenueId, x.IsAvailable });
        });

        b.Entity<ProductTranslation>(e =>
        {
            e.ToTable("product_translations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(10);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description");
            e.HasIndex(x => new { x.ProductId, x.Locale }).IsUnique();
        });

        b.Entity<ProductVariant>(e =>
        {
            e.ToTable("product_variants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
            e.Property(x => x.PriceDeltaMinor).HasColumnName("price_delta_minor");
            e.Property(x => x.IsDefault).HasColumnName("is_default");
            e.Property(x => x.IsAvailable).HasColumnName("is_available");
            e.HasIndex(x => x.ProductId);
        });

        b.Entity<ModifierGroup>(e =>
        {
            e.ToTable("modifier_groups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.MinSelect).HasColumnName("min_select");
            e.Property(x => x.MaxSelect).HasColumnName("max_select");
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.HasIndex(x => x.VenueId);
        });

        b.Entity<ModifierOption>(e =>
        {
            e.ToTable("modifier_options");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ModifierGroupId).HasColumnName("modifier_group_id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.PriceDeltaMinor).HasColumnName("price_delta_minor");
            e.Property(x => x.IsAvailable).HasColumnName("is_available");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.HasIndex(x => x.ModifierGroupId);
        });

        b.Entity<ProductModifierGroup>(e =>
        {
            e.ToTable("product_modifier_groups");
            e.HasKey(x => new { x.ProductId, x.ModifierGroupId });
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ModifierGroupId).HasColumnName("modifier_group_id");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
        });
    }
}
