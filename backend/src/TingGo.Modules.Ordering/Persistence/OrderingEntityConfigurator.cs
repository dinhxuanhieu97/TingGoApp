using Microsoft.EntityFrameworkCore;
using TingGo.Modules.Ordering.Domain;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Ordering.Persistence;

public sealed class OrderingEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<TableSession>(e =>
        {
            e.ToTable("table_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.TableId).HasColumnName("table_id");
            e.Property(x => x.PublicTokenHash).HasColumnName("public_token_hash").HasMaxLength(128);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            e.HasIndex(x => new { x.TableId, x.Status });
            e.HasIndex(x => x.PublicTokenHash).IsUnique();
        });

        b.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.TableSessionId).HasColumnName("table_session_id");
            e.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(32);
            e.Property(x => x.ClientOrderId).HasColumnName("client_order_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.SubtotalMinor).HasColumnName("subtotal_minor");
            e.Property(x => x.DiscountMinor).HasColumnName("discount_minor");
            e.Property(x => x.TaxMinor).HasColumnName("tax_minor");
            e.Property(x => x.TotalMinor).HasColumnName("total_minor");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.CustomerNote).HasColumnName("customer_note");
            e.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            e.Property(x => x.EstimatedReadyAt).HasColumnName("estimated_ready_at");
            e.Property(x => x.PlacedAt).HasColumnName("placed_at");
            e.Property(x => x.CompletedAt).HasColumnName("completed_at");
            e.Property(x => x.RowVersion).HasColumnName("row_version").IsConcurrencyToken();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.VenueId, x.Status, x.PlacedAt });
            e.HasIndex(x => new { x.TableSessionId, x.PlacedAt });
            e.HasIndex(x => new { x.VenueId, x.ClientOrderId }).IsUnique();
        });

        b.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ProductNameSnapshot).HasColumnName("product_name_snapshot").HasMaxLength(200);
            e.Property(x => x.VariantNameSnapshot).HasColumnName("variant_name_snapshot").HasMaxLength(100);
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.UnitPriceMinor).HasColumnName("unit_price_minor");
            e.Property(x => x.ModifierTotalMinor).HasColumnName("modifier_total_minor");
            e.Property(x => x.LineTotalMinor).HasColumnName("line_total_minor");
            e.Property(x => x.Note).HasColumnName("note");
            e.HasIndex(x => x.OrderId);
        });

        b.Entity<OrderItemModifier>(e =>
        {
            e.ToTable("order_item_modifiers");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderItemId).HasColumnName("order_item_id");
            e.Property(x => x.ModifierOptionId).HasColumnName("modifier_option_id");
            e.Property(x => x.OptionNameSnapshot).HasColumnName("option_name_snapshot").HasMaxLength(200);
            e.Property(x => x.Quantity).HasColumnName("quantity");
            e.Property(x => x.UnitPriceMinor).HasColumnName("unit_price_minor");
            e.HasIndex(x => x.OrderItemId);
        });

        b.Entity<ServiceRequest>(e =>
        {
            e.ToTable("service_requests");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.TableSessionId).HasColumnName("table_session_id");
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(32);
            e.Property(x => x.Note).HasColumnName("note");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.RequestedAt).HasColumnName("requested_at");
            e.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
            e.HasIndex(x => new { x.VenueId, x.Status });
        });

        b.Entity<OrderStatusHistory>(e =>
        {
            e.ToTable("order_status_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(32);
            e.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(32);
            e.Property(x => x.ActorMembershipId).HasColumnName("actor_membership_id");
            e.Property(x => x.Reason).HasColumnName("reason");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.OrderId);
        });
    }
}
