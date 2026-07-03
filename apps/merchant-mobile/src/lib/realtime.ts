import { HubConnection, HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { API_ORIGIN, getAccessToken } from "./api";

const ORDER_EVENTS = [
  "order.created", "order.confirmed", "order.rejected",
  "order.preparing", "order.ready", "order.completed", "order.cancelled",
  "service_request.created",
];

export interface RealtimeCallbacks {
  onOrderEvent: (eventType: string, payload: { eventId?: string }) => void;
  onReconnected: () => void;
  onStateChange: (connected: boolean) => void;
}

/** MOB-02/MOB-06: SignalR client + chống lặp eventId + resync khi reconnect. */
export function createRealtimeConnection(venueId: string, callbacks: RealtimeCallbacks): HubConnection {
  const seenEventIds = new Set<string>();
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_ORIGIN}/hubs/orders`, {
      accessTokenFactory: () => getAccessToken() ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build();

  for (const eventType of ORDER_EVENTS) {
    connection.on(eventType, (payload: { eventId?: string }) => {
      if (payload.eventId) {
        if (seenEventIds.has(payload.eventId)) return; // không phát lặp âm/xử lý (MOB-02)
        seenEventIds.add(payload.eventId);
      }
      callbacks.onOrderEvent(eventType, payload);
    });
  }

  connection.onreconnected(() => {
    connection.invoke("JoinVenue", venueId).catch(() => {});
    callbacks.onStateChange(true);
    callbacks.onReconnected(); // gọi API lấy order đang hoạt động, so khớp local (MOB-06)
  });
  connection.onclose(() => callbacks.onStateChange(false));

  connection
    .start()
    .then(() => connection.invoke("JoinVenue", venueId))
    .then(() => callbacks.onStateChange(true))
    .catch(() => callbacks.onStateChange(false));

  return connection;
}
