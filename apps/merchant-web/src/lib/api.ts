const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";

export class ApiError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
    public details?: Record<string, unknown>,
  ) {
    super(message);
  }
}

export function getTokens() {
  if (typeof window === "undefined") return null;
  const accessToken = localStorage.getItem("tg_access");
  const refreshToken = localStorage.getItem("tg_refresh");
  return accessToken && refreshToken ? { accessToken, refreshToken } : null;
}

export function saveTokens(accessToken: string, refreshToken: string) {
  localStorage.setItem("tg_access", accessToken);
  localStorage.setItem("tg_refresh", refreshToken);
}

export function clearTokens() {
  localStorage.removeItem("tg_access");
  localStorage.removeItem("tg_refresh");
}

async function tryRefresh(): Promise<boolean> {
  const tokens = getTokens();
  if (!tokens) return false;
  const res = await fetch(`${API_URL}/auth/refresh`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken: tokens.refreshToken }),
  });
  if (!res.ok) {
    clearTokens();
    return false;
  }
  const data = await res.json();
  saveTokens(data.accessToken, data.refreshToken);
  return true;
}

export async function api<T = unknown>(
  path: string,
  options: { method?: string; body?: unknown; formData?: FormData } = {},
  retried = false,
): Promise<T> {
  const tokens = getTokens();
  const headers: Record<string, string> = {};
  if (tokens) headers.Authorization = `Bearer ${tokens.accessToken}`;
  if (options.body !== undefined) headers["Content-Type"] = "application/json";

  const res = await fetch(`${API_URL}${path}`, {
    method: options.method ?? (options.body !== undefined || options.formData ? "POST" : "GET"),
    headers,
    body: options.formData ?? (options.body !== undefined ? JSON.stringify(options.body) : undefined),
  });

  if (res.status === 401 && !retried && !path.startsWith("/auth/")) {
    if (await tryRefresh()) return api<T>(path, options, true);
    if (typeof window !== "undefined") window.location.href = "/login";
    throw new ApiError("UNAUTHORIZED", "Phiên đăng nhập hết hạn.", 401);
  }

  if (res.status === 204) return undefined as T;

  const text = await res.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    throw new ApiError(
      data?.code ?? "UNKNOWN",
      data?.message ?? "Đã xảy ra lỗi.",
      res.status,
      data?.details,
    );
  }
  return data as T;
}
