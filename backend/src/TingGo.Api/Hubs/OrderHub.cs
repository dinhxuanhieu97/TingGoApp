using Microsoft.AspNetCore.SignalR;

namespace TingGo.Api.Hubs;

/// <summary>
/// Hub real-time /hubs/orders (PRD mục 8).
/// Group theo venue cho merchant, theo table session cho khách.
/// Sprint 6: outbox worker sẽ phát các sự kiện order.*, service_request.*, payment.*.
/// </summary>
public sealed class OrderHub : Hub
{
    private static string VenueGroup(string venueId) => $"venue:{venueId}";
    private static string SessionGroup(string token) => $"session:{token}";

    public Task JoinVenue(string venueId)
        => Groups.AddToGroupAsync(Context.ConnectionId, VenueGroup(venueId));
        // TODO Sprint 6: xác thực membership trước khi join.

    public Task LeaveVenue(string venueId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, VenueGroup(venueId));

    public Task JoinTableSession(string publicSessionToken)
        => Groups.AddToGroupAsync(Context.ConnectionId, SessionGroup(publicSessionToken));

    public Task LeaveTableSession(string publicSessionToken)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, SessionGroup(publicSessionToken));
}
