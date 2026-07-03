import { useCallback, useEffect, useState } from "react";
import {
  FlatList, RefreshControl, StyleSheet, Switch, Text, TextInput, View,
} from "react-native";
import { api, ApiError } from "../lib/api";
import { formatMoney, Product } from "../lib/types";

/** MOB-04: bật/tắt món nhanh không cần mở form chỉnh sửa. */
export default function ProductsScreen({ venueId }: { venueId: string }) {
  const [products, setProducts] = useState<Product[]>([]);
  const [search, setSearch] = useState("");
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState("");

  const load = useCallback(async () => {
    try {
      const query = search.trim() ? `?search=${encodeURIComponent(search.trim())}` : "";
      setProducts(await api<Product[]>(`/venues/${venueId}/products${query}`));
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không tải được danh sách món.");
    }
  }, [venueId, search]);

  useEffect(() => {
    const timer = setTimeout(load, 300); // debounce tìm kiếm
    return () => clearTimeout(timer);
  }, [load]);

  async function toggle(product: Product) {
    // Optimistic update — trải nghiệm tức thì (MOB-04)
    setProducts((prev) =>
      prev.map((p) => (p.id === product.id ? { ...p, isAvailable: !p.isAvailable } : p)),
    );
    try {
      await api(`/products/${product.id}/availability`, {
        method: "PATCH",
        body: { isAvailable: !product.isAvailable },
      });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không cập nhật được.");
      await load(); // rollback theo server
    }
  }

  return (
    <View style={styles.container}>
      <TextInput
        style={styles.search}
        placeholder="Tìm món..."
        value={search}
        onChangeText={setSearch}
      />
      {error !== "" && <Text style={styles.error}>{error}</Text>}
      <FlatList
        data={products}
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
        ListEmptyComponent={<Text style={styles.empty}>Không có món nào.</Text>}
        renderItem={({ item }) => (
          <View style={styles.row}>
            <View style={styles.info}>
              <Text style={[styles.name, !item.isAvailable && styles.unavailable]}>{item.name}</Text>
              <Text style={styles.price}>{formatMoney(item.basePriceMinor, item.currencyCode)}</Text>
            </View>
            <Switch
              value={item.isAvailable}
              onValueChange={() => toggle(item)}
              trackColor={{ true: "#ea580c" }}
            />
          </View>
        )}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  search: {
    backgroundColor: "#fff", margin: 12, borderRadius: 12, padding: 12,
    borderWidth: 1, borderColor: "#e5e7eb",
  },
  error: { color: "#dc2626", textAlign: "center", marginBottom: 8 },
  empty: { textAlign: "center", color: "#9ca3af", marginTop: 40 },
  row: {
    flexDirection: "row", alignItems: "center", backgroundColor: "#fff",
    marginHorizontal: 12, marginBottom: 8, borderRadius: 14, padding: 12,
  },
  info: { flex: 1 },
  name: { fontSize: 15, fontWeight: "600" },
  unavailable: { color: "#9ca3af", textDecorationLine: "line-through" },
  price: { color: "#6b7280", fontSize: 13 },
});
