namespace TingGo.SharedKernel.Contracts;

/// <summary>Contract cross-module (impl tại Venues) — tra cứu venue không tham chiếu module trực tiếp.</summary>
public interface IVenueDirectory
{
    /// <summary>Trả về organization_id của venue, null nếu venue không tồn tại.</summary>
    Task<Guid?> GetOrganizationIdAsync(Guid venueId, CancellationToken ct = default);
}
