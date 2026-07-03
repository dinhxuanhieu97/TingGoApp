import { useCallback, useEffect, useRef, useState } from "react";
import {
  AppState, FlatList, RefreshControl, StyleSheet, Text, TouchableOpacity, View,
} from "react-native";
import type { HubConnection } from "@microsoft/signalr";
import { api, ApiError } from "../lib/api";
import { createRealtimeConnection } from "../lib/realtime";
import { orderAnnouncement, tts } from "../lib/tts";
import {
  ActiveOrder, formatMoney, NEXT_ACTION, OrderView, STATUS_LABEL,
} from "../lib/types";

interface Props {
  venueId: string;
  tableCodes: Record<string, string>;
  ttsEnabled: boolean;
}

export default function OrdersScreen({ venueId, tableCodes, ttsEnabled }: Props) {
  const [orders, setOrders] = useState<ActiveOrder[]>([]);
  const [connected, setConnected] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");
  const connectionRef = useRef<HubConnection | null>(null);
  const ttsEnabledRef = useRef(ttsEnabled);
  ttsEnabledRef.current = ttsEnabled;

  const load = useCallback(async () => {
    try {
      setOrders(await api<ActiveOrder[]>(`/venues/${venueId}/orders/active`));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không tải được order.");
    }
  }, [venueId]);

  // SignalR + TTS đơn mới (MOB-02/05)
  useEffect(() => {
    load();
    const connection = createRealtimeConnection(venueId, {
      onOrderEvent: async (eventType) => {
        if (eventType === "order.created" && ttsEnabledRef.current) {
          // Lấy order mới nhất để đọc — payload chỉ có tóm tắt
          try {
            const latest = await api<ActiveOrder[]>(`/venues/${venueId}/orders/active`);
            setOrders(latest);
            const newest = latest[latest.length - 1];
            if (newest) {
              const tableCode = newest.tableId ? (tableCodes[newest.tableId] ?? "?") : "?";
              await tts.speak(orderAnnouncement(tableCode, newest.view.items));
            }
            return;
          } catch {
            /* fallthrough to load */
          }
        }
        load();
      },
      onReconnected: load, // MOB-06: resync snapshot
      onStateChange: setConnected,
    });
    connectionRef.current = connection;
    return () => {
      connection.stop();
    };
  }, [venueId, load, tableCodes]);

  // MOB-06: app quay lại foreground → resync
  useEffect(() => {
    const sub = AppState.addEventListener("change", (state) => {
      if (state === "active") load();
    });
    return () => sub.remove();
  }, [load]);

  async function advance(order: OrderView, action: string) {
    try {
      await api(`/orders/${order.id}/${action}`, { body: { rowVersion: order.rowVersion } });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không cập nhật được.");
      await load();
    }
  }

  return (
    <View style={styles.container}>
      <View style={styles.statusBar}>
        <View style={[styles.dot, { backgroundColor: connected ? "#22c55e" : "#9ca3af" }]} />
        <Text style={styles.statusText}>{connected ? "Real-time" : "Đang kết nối..."}</Text>
        <Text style={styles.count}>{orders.length} order</Text>
      </View>
      {error !== "" && (
        <TouchableOpacity onPress={() => setError("")}>
          <Text style={styles.error}>{error} ✕</Text>
        </TouchableOpacity>
      )}
      <FlatList
        data={orders}
        keyExtractor={(item) => item.view.id}
        refreshControl={
          <RefreshControl
            refreshing={refreshing}
            onRefresh={async () => {
              setRefreshing(true);
              await load();
              setRefreshing(false);
            }}
          />
        }
        ListEmptyComponent={<Text style={styles.empty}>Chưa có order nào đang hoạt động.</Text>}
        renderItem={({ item }) => {
          const order = item.view;
          const next = NEXT_ACTION[order.status];
          return (
            <View style={styles.card}>
              <View style={styles.cardHeader}>
                <Text style={styles.orderNumber}>{order.orderNumber}</Text>
                <Text style={styles.table}>
                  Bàn {item.tableId ? (tableCodes[item.tableId] ?? "?") : "?"}
                </Text>
                <Text style={styles.badge}>{STATUS_LABEL[order.status] ?? order.status}</Text>
              </View>
              {order.items.map((orderItem, index) => (
                <Text key={index} style={styles.item}>
                  {orderItem.quantity}× {orderItem.productName}
                  {orderItem.variantName ? ` (${orderItem.variantName})` : ""}
                  {orderItem.options.length > 0 ? ` — ${orderItem.options.join(", ")}` : ""}
                  {orderItem.note ? ` “${orderItem.note}”` : ""}
                </Text>
              ))}
              {order.customerNote ? (
                <Text style={styles.note}>Ghi chú: {order.customerNote}</Text>
              ) : null}
              <View style={styles.cardFooter}>
                <Text style={styles.total}>{formatMoney(order.totalMinor, order.currencyCode)}</Text>
                <View style={styles.actions}>
                  {order.status === "submitted" && (
                    <TouchableOpacity
                      style={[styles.actionButton, styles.rejectButton]}
                      onPress={() => advance(order, "reject")}
                    >
                      <Text style={styles.actionText}>Từ chối</Text>
                    </TouchableOpacity>
                  )}
                  {next && (
                    <TouchableOpacity
                      style={styles.actionButton}
                      onPress={() => advance(order, next.action)}
                    >
                      <Text style={styles.actionText}>{next.label}</Text>
                    </TouchableOpacity>
                  )}
                </View>
              </View>
            </View>
          );
        }}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  statusBar: { flexDirection: "row", alignItems: "center", padding: 12, gap: 6 },
  dot: { width: 8, height: 8, borderRadius: 4 },
  statusText: { color: "#6b7280", fontSize: 12 },
  count: { marginLeft: "auto", color: "#6b7280", fontSize: 12 },
  error: { color: "#dc2626", padding: 8, textAlign: "center" },
  empty: { textAlign: "center", color: "#9ca3af", marginTop: 40 },
  card: {
    backgroundColor: "#fff", marginHorizontal: 12, marginBottom: 10,
    borderRadius: 16, padding: 12, shadowOpacity: 0.05, shadowRadius: 4, elevation: 1,
  },
  cardHeader: { flexDirection: "row", alignItems: "center", gap: 8, marginBottom: 6 },
  orderNumber: { fontWeight: "700", fontSize: 16 },
  table: { backgroundColor: "#f3f4f6", paddingHorizontal: 8, paddingVertical: 2, borderRadius: 8, fontSize: 12 },
  badge: {
    marginLeft: "auto", backgroundColor: "#ffedd5", color: "#9a3412",
    paddingHorizontal: 8, paddingVertical: 2, borderRadius: 8, fontSize: 12, overflow: "hidden",
  },
  item: { fontSize: 14, marginBottom: 2 },
  note: { fontStyle: "italic", color: "#ea580c", fontSize: 12, marginTop: 4 },
  cardFooter: { flexDirection: "row", alignItems: "center", marginTop: 8 },
  total: { fontWeight: "700", color: "#ea580c" },
  actions: { flexDirection: "row", gap: 8, marginLeft: "auto" },
  actionButton: { backgroundColor: "#ea580c", borderRadius: 10, paddingHorizontal: 14, paddingVertical: 8 },
  rejectButton: { backgroundColor: "#ef4444" },
  actionText: { color: "#fff", fontWeight: "600", fontSize: 13 },
});
