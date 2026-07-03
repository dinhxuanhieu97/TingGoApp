import { useState } from "react";
import {
  ActivityIndicator, KeyboardAvoidingView, Platform, StyleSheet,
  Text, TextInput, TouchableOpacity, View,
} from "react-native";
import { api, ApiError, saveTokens } from "../lib/api";

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export default function LoginScreen({ onLoggedIn }: { onLoggedIn: () => void }) {
  const [mode, setMode] = useState<"owner" | "staff">("owner");
  const [step, setStep] = useState<"email" | "code">("email");
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [venueId, setVenueId] = useState("");
  const [staffCode, setStaffCode] = useState("");
  const [pin, setPin] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function run(action: () => Promise<void>) {
    setError("");
    setLoading(true);
    try {
      await action();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");
    } finally {
      setLoading(false);
    }
  }

  const requestOtp = () =>
    run(async () => {
      await api("/auth/otp/request", { body: { email } });
      setStep("code");
    });

  const verifyOtp = () =>
    run(async () => {
      const tokens = await api<AuthTokens>("/auth/otp/verify", {
        body: { email, code, deviceName: "merchant-mobile" },
      });
      await saveTokens(tokens.accessToken, tokens.refreshToken);
      onLoggedIn();
    });

  const staffLogin = () =>
    run(async () => {
      const tokens = await api<AuthTokens>("/auth/staff/login", {
        body: { venueId, staffCode, pin, deviceName: "merchant-mobile" },
      });
      await saveTokens(tokens.accessToken, tokens.refreshToken);
      onLoggedIn();
    });

  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === "ios" ? "padding" : undefined}
    >
      <Text style={styles.logo}>TingGo</Text>
      <Text style={styles.tagline}>Quét bàn, gọi món, quán nhận ngay.</Text>

      <View style={styles.tabs}>
        <TouchableOpacity
          style={[styles.tab, mode === "owner" && styles.tabActive]}
          onPress={() => setMode("owner")}
        >
          <Text style={mode === "owner" ? styles.tabTextActive : styles.tabText}>Chủ quán</Text>
        </TouchableOpacity>
        <TouchableOpacity
          style={[styles.tab, mode === "staff" && styles.tabActive]}
          onPress={() => setMode("staff")}
        >
          <Text style={mode === "staff" ? styles.tabTextActive : styles.tabText}>Nhân viên</Text>
        </TouchableOpacity>
      </View>

      {mode === "owner" ? (
        step === "email" ? (
          <>
            <TextInput
              style={styles.input}
              placeholder="Email chủ quán"
              autoCapitalize="none"
              keyboardType="email-address"
              value={email}
              onChangeText={setEmail}
            />
            <TouchableOpacity style={styles.button} onPress={requestOtp} disabled={loading}>
              {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Gửi mã OTP</Text>}
            </TouchableOpacity>
          </>
        ) : (
          <>
            <Text style={styles.hint}>Mã OTP đã gửi tới {email}</Text>
            <TextInput
              style={[styles.input, styles.codeInput]}
              placeholder="Mã 6 số"
              keyboardType="number-pad"
              maxLength={6}
              value={code}
              onChangeText={setCode}
            />
            <TouchableOpacity style={styles.button} onPress={verifyOtp} disabled={loading}>
              {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Đăng nhập</Text>}
            </TouchableOpacity>
            <TouchableOpacity onPress={() => setStep("email")}>
              <Text style={styles.link}>Đổi email</Text>
            </TouchableOpacity>
          </>
        )
      ) : (
        <>
          <TextInput
            style={styles.input}
            placeholder="Venue ID (chủ quán cung cấp)"
            autoCapitalize="none"
            value={venueId}
            onChangeText={setVenueId}
          />
          <TextInput
            style={styles.input}
            placeholder="Mã nhân viên (VD: NV01)"
            autoCapitalize="characters"
            value={staffCode}
            onChangeText={setStaffCode}
          />
          <TextInput
            style={styles.input}
            placeholder="PIN"
            keyboardType="number-pad"
            secureTextEntry
            maxLength={6}
            value={pin}
            onChangeText={setPin}
          />
          <TouchableOpacity style={styles.button} onPress={staffLogin} disabled={loading}>
            {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Đăng nhập</Text>}
          </TouchableOpacity>
        </>
      )}

      {error !== "" && <Text style={styles.error}>{error}</Text>}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: "center", padding: 24, backgroundColor: "#fff7ed" },
  logo: { fontSize: 32, fontWeight: "bold", color: "#ea580c", textAlign: "center" },
  tagline: { textAlign: "center", color: "#6b7280", marginBottom: 24 },
  tabs: { flexDirection: "row", marginBottom: 16, borderRadius: 12, backgroundColor: "#ffedd5" },
  tab: { flex: 1, padding: 10, borderRadius: 12, alignItems: "center" },
  tabActive: { backgroundColor: "#ea580c" },
  tabText: { color: "#9a3412", fontWeight: "600" },
  tabTextActive: { color: "#fff", fontWeight: "700" },
  input: {
    backgroundColor: "#fff", borderRadius: 12, padding: 14, marginBottom: 12,
    borderWidth: 1, borderColor: "#e5e7eb", fontSize: 16,
  },
  codeInput: { textAlign: "center", fontSize: 24, letterSpacing: 8 },
  button: { backgroundColor: "#ea580c", borderRadius: 12, padding: 14, alignItems: "center" },
  buttonText: { color: "#fff", fontWeight: "700", fontSize: 16 },
  link: { color: "#6b7280", textAlign: "center", marginTop: 12 },
  hint: { color: "#6b7280", marginBottom: 8, textAlign: "center" },
  error: { color: "#dc2626", marginTop: 12, textAlign: "center" },
});
