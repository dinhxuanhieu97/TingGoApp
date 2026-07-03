namespace TingGo.SharedKernel.Contracts;

public sealed record VenueInfo(
    Guid Id, Guid OrganizationId, string CurrencyCode, string DefaultLocale,
    string Timezone, string Status, string Name = "", string Slug = "");

/// <summary>Contract cross-module (impl tại Venues) — tra cứu venue không tham chiếu module trực tiếp.</summary>
public interface IVenueDirectory
{
    /// <summary>Trả về organization_id của venue, null nếu venue không tồn tại.</summary>
    Task<Guid?> GetOrganizationIdAsync(Guid venueId, CancellationToken ct = default);

    /// <summary>Thông tin cấu hình venue (currency, locale, timezone) — null nếu không tồn tại.</summary>
    Task<VenueInfo?> GetVenueInfoAsync(Guid venueId, CancellationToken ct = default);

    /// <summary>Tra cứu venue theo public slug — null nếu không tồn tại.</summary>
    Task<VenueInfo?> GetVenueBySlugAsync(string slug, CancellationToken ct = default);
}
