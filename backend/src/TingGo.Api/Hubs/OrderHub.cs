using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using TingGo.SharedKernel.Contracts;

namespace TingGo.Api.Hubs;

/// <summary>
/// Hub real-time /hubs/orders (PRD mục 8).
/// Merchant join group venue (yêu cầu JWT + membership); khách join group session (token là bí mật).
/// </summary>
public sealed class OrderHub(IVenueDirectory venues, IMembershipService memberships) : Hub
{
    private static string VenueGroup(Guid venueId) => $"venue:{venueId}";
    private static string SessionGroup(string token) => $"session:{token}";

    public async Task JoinVenue(Guid venueId)
    {
        var sub = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? Context.User?.FindFirstValue("sub")
                  ?? throw new HubException("UNAUTHORIZED: cần đăng nhập để theo dõi quán.");

        var organizationId = await venues.GetOrganizationIdAsync(venueId)
            ?? throw new HubException("NOT_FOUND: quán không tồn tại.");
        var role = await memberships.GetOrganizationRoleAsync(Guid.Parse(sub), organizationId)
            ?? throw new HubException("FORBIDDEN: bạn không thuộc quán này.");

        await Groups.AddToGroupAsync(Context.ConnectionId, VenueGroup(venueId));
    }

    public Task LeaveVenue(Guid venueId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, VenueGroup(venueId));

    public Task JoinTableSession(string publicSessionToken)
        => Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(publicSessionToken));

    public Task LeaveTableSession(string publicSessionToken)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(publicSessionToken));
}
