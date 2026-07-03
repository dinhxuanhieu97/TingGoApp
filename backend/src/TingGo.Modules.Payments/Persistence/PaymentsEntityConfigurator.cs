using Microsoft.EntityFrameworkCore;
using TingGo.Modules.Payments.Domain;
using TingGo.SharedKernel.Persistence;

namespace TingGo.Modules.Payments.Persistence;

public sealed class PaymentsEntityConfigurator : IModuleEntityConfigurator
{
    public void Configure(ModelBuilder b)
    {
        b.Entity<Payment>(e =>
        {
            e.ToTable("payments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.VenueId).HasColumnName("venue_id");
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.TableSessionId).HasColumnName("table_session_id");
            e.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(32);
            e.Property(x => x.ProviderPaymentId).HasColumnName("provider_payment_id").HasMaxLength(200);
            e.Property(x => x.Method).HasColumnName("method").HasMaxLength(32);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(32);
            e.Property(x => x.AmountMinor).HasColumnName("amount_minor");
            e.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3).IsFixedLength();
            e.Property(x => x.PaidAt).HasColumnName("paid_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.VenueId, x.Status });
            e.HasIndex(x => x.TableSessionId);
        });
    }
}
