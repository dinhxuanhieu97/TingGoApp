import { useCallback, useEffect, useState } from "react";
import { StatusBar } from "expo-status-bar";
import {
  ActivityIndicator, Platform, StyleSheet, Switch, Text, TouchableOpacity, View,
} from "react-native";
import * as Notifications from "expo-notifications";
import { api, clearTokens, loadTokens } from "./src/lib/api";
import type { Membership, Venue } from "./src/lib/types";
import LoginScreen from "./src/screens/LoginScreen";
import OrdersScreen from "./src/screens/OrdersScreen";
import ProductsScreen from "./src/screens/ProductsScreen";
import RequestsScreen from "./src/screens/RequestsScreen";

type Screen = "loading" | "login" | "main";
type Tab = "orders" | "requests" | "products";

/** MOB-02: đăng ký Expo push token — thất bại thầm lặng trên môi trường không hỗ trợ (Expo Go Android). */
async function registerPushToken(): Promise<void> {
  try {
    const permission = await Notifications.requestPermissionsAsync();
    if (!permission.granted) return;
    const token = (await Notifications.getExpoPushTokenAsync()).data;
    await api("/me/devices", {
      body: { platform: Platform.OS === "ios" ? "ios" : "android", token, deviceName: "expo" },
    });
  } catch {
    /* push chưa khả dụng — SignalR + TTS vẫn hoạt động khi app mở */
  }
}

export default function App() {
  const [screen, setScreen] = useState<Screen>("loading");
  const [tab, setTab] = useState<Tab>("orders");
  const [venue, setVenue] = useState<Venue | null>(null);
  const [tableCodes, setTableCodes] = useState<Record<string, string>>({});
  const [ttsEnabled, setTtsEnabled] = useState(true);
  const [requestsSignal, setRequestsSignal] = useState(0);

  const bootstrap = useCallback(async () => {
    try {
      const memberships = await api<Membership[]>("/me/memberships");
      if (memberships.length === 0) {
        setScreen("login");
        return;
      }
      // Owner: membership venueId null → lấy venue đầu của org; staff: venue trực tiếp
      const membership = memberships[0];
      let selected: Venue | null = null;
      if (membership.venueId) {
        selected = await api<Venue>(`/venues/${membership.venueId}`);
      } else {
        const venues = await api<Venue[]>(`/organizations/${membership.organizationId}/venues`);
        selected = venues[0] ?? null;
      }
      if (!selected) {
        setScreen("login");
        return;
      }
      setVenue(selected);
      const tables = await api<{ id: string; code: string }[]>(`/venues/${selected.id}/tables`);
      setTableCodes(Object.fromEntries(tables.map((t) => [t.id, t.code])));
      setScreen("main");
      registerPushToken();
    } catch {
      await clearTokens();
      setScreen("login");
    }
  }, []);

  useEffect(() => {
    (async () => {
      if (await loadTokens()) {
        await bootstrap();
      } else {
        setScreen("login");
      }
    })();
  }, [bootstrap]);

  if (screen === "loading") {
    return (
      <View style={styles.center}>
        <ActivityIndicator size="large" color="#ea580c" />
      </View>
    );
  }

  if (screen === "login") {
    return (
      <>
        <LoginScreen onLoggedIn={bootstrap} />
        <StatusBar style="dark" />
      </>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.header}>
        <Text style={styles.logo}>TingGo</Text>
        <Text style={styles.venueName} numberOfLines={1}>{venue?.name}</Text>
        <View style={styles.ttsToggle}>
          <Text style={styles.ttsLabel}>TTS</Text>
          <Switch value={ttsEnabled} onValueChange={setTtsEnabled} trackColor={{ true: "#ea580c" }} />
        </View>
        <TouchableOpacity
          onPress={async () => {
            await clearTokens();
            setScreen("login");
          }}
        >
          <Text style={styles.logout}>Thoát</Text>
        </TouchableOpacity>
      </View>

      {venue && (
        <View style={[styles.screenHost, tab !== "orders" && styles.hidden]}>
          <OrdersScreen
            venueId={venue.id}
            tableCodes={tableCodes}
            ttsEnabled={ttsEnabled}
            onServiceRequestEvent={() => setRequestsSignal((n) => n + 1)}
          />
        </View>
      )}
      {venue && (
        <View style={[styles.screenHost, tab !== "requests" && styles.hidden]}>
          <RequestsScreen venueId={venue.id} tableCodes={tableCodes} refreshSignal={requestsSignal} />
        </View>
      )}
      {venue && tab === "products" && <ProductsScreen venueId={venue.id} />}

      <View style={styles.tabBar}>
        <TouchableOpacity
          style={[styles.tabItem, tab === "orders" && styles.tabItemActive]}
          onPress={() => setTab("orders")}
        >
          <Text style={tab === "orders" ? styles.tabTextActive : styles.tabText}>📋 Order</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabItem, tab === "requests" && styles.tabItemActive]}
          onPress={() => setTab("requests")}
        >
          <Text style={tab === "requests" ? styles.tabTextActive : styles.tabText}>🙋 Yêu cầu</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tabItem, tab === "products" && styles.tabItemActive]}
          onPress={() => setTab("products")}
        >
          <Text style={tab === "products" ? styles.tabTextActive : styles.tabText}>🍽 Món</Text>
        </TouchableOpacity>
      </View>
      <StatusBar style="dark" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#fff7ed", paddingTop: 48 },
  center: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: "#fff7ed" },
  header: {
    flexDirection: "row", alignItems: "center", paddingHorizontal: 16, paddingBottom: 8, gap: 10,
  },
  logo: { fontSize: 20, fontWeight: "bold", color: "#ea580c" },
  venueName: { flex: 1, color: "#6b7280", fontSize: 13 },
  ttsToggle: { flexDirection: "row", alignItems: "center", gap: 4 },
  ttsLabel: { fontSize: 11, color: "#6b7280" },
  logout: { color: "#9ca3af", fontSize: 13 },
  screenHost: { flex: 1 },
  hidden: { display: "none" },
  tabBar: {
    flexDirection: "row", backgroundColor: "#fff", borderTopWidth: 1,
    borderTopColor: "#f3f4f6", paddingBottom: 24,
  },
  tabItem: { flex: 1, alignItems: "center", paddingVertical: 12 },
  tabItemActive: { borderTopWidth: 2, borderTopColor: "#ea580c" },
  tabText: { color: "#9ca3af", fontWeight: "600" },
  tabTextActive: { color: "#ea580c", fontWeight: "700" },
});
