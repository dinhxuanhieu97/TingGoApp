import { useCallback, useEffect, useState } from "react";
import { FlatList, RefreshControl, StyleSheet, Text, TouchableOpacity, View } from "react-native";
import { api, ApiError } from "../lib/api";

interface ServiceRequestItem {
  id: string;
  type: string;
  note?: string;
  status: string;
  requestedAt: string;
  tableId: string;
}

const TYPE_LABEL: Record<string, string> = {
  call_staff: "🔔 Gọi nhân viên",
  supplies: "🥢 Xin thêm đồ",
  payment: "💰 Thanh toán",
};

interface Props {
  venueId: string;
  tableCodes: Record<string, string>;
  refreshSignal: number; // tăng khi có service_request.* event từ SignalR
}

/** Tab Yêu cầu (Sprint 7 mobile): danh sách khách gọi + xử lý.</summary> */
export default function RequestsScreen({ venueId, tableCodes, refreshSignal }: Props) {
  const [requests, setRequests] = useState<ServiceRequestItem[]>([]);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      setRequests(await api<ServiceRequestItem[]>(`/venues/${venueId}/service-requests`));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không tải được yêu cầu.");
    }
  }, [venueId]);

  useEffect(() => {
    load();
  }, [load, refreshSignal]);

  async function act(request: ServiceRequestItem, action: "acknowledge" | "resolve") {
    try {
      await api(`/service-requests/${request.id}/${action}`, { method: "POST" });
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không cập nhật được.");
      await load();
    }
  }

  return (
    <View style={styles.container}>
      {error !== "" && (
        <TouchableOpacity onPress={() => setError("")}>
          <Text style={styles.error}>{error} ✕</Text>
        </TouchableOpacity>
      )}
      <FlatList
        data={requests}
        keyExtractor={(item) => item.id}
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
        ListEmptyComponent={<Text style={styles.empty}>Không có yêu cầu nào đang chờ 🎉</Text>}
        renderItem={({ item }) => (
          <View style={styles.card}>
            <View style={styles.cardHeader}>
              <Text style={styles.table}>Bàn {tableCodes[item.tableId] ?? "?"}</Text>
              <Text style={styles.type}>{TYPE_LABEL[item.type] ?? item.type}</Text>
              <Text style={styles.time}>
                {new Date(item.requestedAt).toLocaleTimeString("vi-VN", { hour: "2-digit", minute: "2-digit" })}
              </Text>
            </View>
            {item.note ? <Text style={styles.note}>“{item.note}”</Text> : null}
            <View style={styles.actions}>
              {item.status === "pending" && (
                <TouchableOpacity style={[styles.button, styles.ackButton]} onPress={() => act(item, "acknowledge")}>
                  <Text style={styles.buttonText}>Đã nhận</Text>
                </TouchableOpacity>
              )}
              <TouchableOpacity style={styles.button} onPress={() => act(item, "resolve")}>
                <Text style={styles.buttonText}>Xong ✓</Text>
              </TouchableOpacity>
            </View>
          </View>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, paddingTop: 8 },
  error: { color: "#dc2626", padding: 8, textAlign: "center" },
  empty: { textAlign: "center", color: "#9ca3af", marginTop: 40 },
  card: {
    backgroundColor: "#fff", marginHorizontal: 12, marginBottom: 10,
    borderRadius: 16, padding: 12,
  },
  cardHeader: { flexDirection: "row", alignItems: "center", gap: 8 },
  table: { fontWeight: "700", fontSize: 15 },
  type: { fontSize: 14 },
  time: { marginLeft: "auto", color: "#9ca3af", fontSize: 12 },
  note: { fontStyle: "italic", color: "#6b7280", marginTop: 4, fontSize: 13 },
  actions: { flexDirection: "row", gap: 8, marginTop: 10 },
  button: { flex: 1, backgroundColor: "#16a34a", borderRadius: 10, paddingVertical: 8, alignItems: "center" },
  ackButton: { backgroundColor: "#f59e0b" },
  buttonText: { color: "#fff", fontWeight: "600", fontSize: 13 },
});
