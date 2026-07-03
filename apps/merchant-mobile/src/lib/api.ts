import * as SecureStore from "expo-secure-store";

// Máy thật/emulator: đổi bằng biến môi trường EXPO_PUBLIC_API_URL (IP LAN của máy dev)
export const API_URL = process.env.EXPO_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";
export const API_ORIGIN = API_URL.replace("/api/v1", "");

const ACCESS_KEY = "tg_access";
const REFRESH_KEY = "tg_refresh";

let accessToken: string | null = null;

export class ApiError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

export async function loadTokens(): Promise<boolean> {
  accessToken = await SecureStore.getItemAsync(ACCESS_KEY);
  return accessToken !== null;
}

export async function saveTokens(access: string, refresh: string): Promise<void> {
  accessToken = access;
  await SecureStore.setItemAsync(ACCESS_KEY, access);
  await SecureStore.setItemAsync(REFRESH_KEY, refresh);
}

export async function clearTokens(): Promise<void> {
  accessToken = null;
  await SecureStore.deleteItemAsync(ACCESS_KEY);
  await SecureStore.deleteItemAsync(REFRESH_KEY);
}

export function getAccessToken(): string | null {
  return accessToken;
}

async function tryRefresh(): Promise<boolean> {
  const refreshToken = await SecureStore.getItemAsync(REFRESH_KEY);
  if (!refreshToken) return false;
  const res = await fetch(`${API_URL}/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });
  if (!res.ok) {
    await clearTokens();
    return false;
  }
  const data = await res.json();
  await saveTokens(data.accessToken, data.refreshToken);
  return true;
}

export async function api<T = unknown>(
  path: string,
  options: { method?: string; body?: unknown } = {},
  retried = false,
): Promise<T> {
  const headers: Record<string, string> = {};
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  if (options.body !== undefined) headers["Content-Type"] = "application/json";

  const res = await fetch(`${API_URL}${path}`, {
    method: options.method ?? (options.body !== undefined ? "POST" : "GET"),
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  if (res.status === 401 && !retried && !path.startsWith("/auth/")) {
    if (await tryRefresh()) return api<T>(path, options, true);
    throw new ApiError("UNAUTHORIZED", "Phiên đăng nhập hết hạn.", 401);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    throw new ApiError(data?.code ?? "UNKNOWN", data?.message ?? "Đã xảy ra lỗi.", res.status);
  }
  return data as T;
}
