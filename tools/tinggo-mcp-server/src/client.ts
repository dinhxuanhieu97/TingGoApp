/**
 * TingGo API client cho MCP server (dev/QA local).
 * Login OTP tự động qua Mailpit — CHỈ dùng cho môi trường dev.
 */

export const API_URL = process.env.TINGGO_API_URL ?? "http://localhost:5080/api/v1";
export const MAILPIT_URL = process.env.TINGGO_MAILPIT_URL ?? "http://localhost:8025";

let accessToken: string | null = null;
let refreshToken: string | null = null;

export class TingGoError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

export async function api<T = unknown>(
  path: string,
  options: { method?: string; body?: unknown; headers?: Record<string, string>; auth?: boolean } = {},
): Promise<T> {
  const headers: Record<string, string> = { ...options.headers };
  if (options.body !== undefined) headers["Content-Type"] = "application/json";
  if (options.auth !== false && accessToken) headers.Authorization = `Bearer ${accessToken}`;

  const res = await fetch(`${API_URL}${path}`, {
    method: options.method ?? (options.body !== undefined ? "POST" : "GET"),
    headers,
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });

  const text = await res.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    throw new TingGoError(
      data?.code ?? "UNKNOWN",
      data?.message ?? `HTTP ${res.status}`,
      res.status,
    );
  }
  return data as T;
}

/** Đăng nhập owner: gửi OTP → đọc mã từ Mailpit → verify. Chỉ hoạt động với Mailpit local. */
export async function loginViaOtp(email: string): Promise<{ userId: string; email?: string }> {
  await api("/auth/otp/request", { body: { email }, auth: false });

  // Chờ mail đến Mailpit rồi đọc mã 6 số
  let code: string | null = null;
  for (let attempt = 0; attempt < 10 && !code; attempt++) {
    await new Promise((r) => setTimeout(r, 500));
    const list = (await (await fetch(`${MAILPIT_URL}/api/v1/messages?limit=1`)).json()) as {
      messages?: { ID: string; To?: { Address: string }[] }[];
    };
    const msg = list.messages?.[0];
    if (!msg || !msg.To?.some((t) => t.Address.toLowerCase() === email.toLowerCase())) continue;
    const detail = (await (await fetch(`${MAILPIT_URL}/api/v1/message/${msg.ID}`)).json()) as {
      Text?: string;
    };
    code = detail.Text?.match(/\d{6}/)?.[0] ?? null;
  }
  if (!code) {
    throw new TingGoError("OTP_NOT_FOUND",
      `Không đọc được OTP cho ${email} từ Mailpit (${MAILPIT_URL}). Docker compose có đang chạy không?`, 500);
  }

  const tokens = await api<{ accessToken: string; refreshToken: string; userId: string; email?: string }>(
    "/auth/otp/verify",
    { body: { email, code, deviceName: "tinggo-mcp-server" }, auth: false },
  );
  accessToken = tokens.accessToken;
  refreshToken = tokens.refreshToken;
  return { userId: tokens.userId, email: tokens.email };
}

export function isLoggedIn(): boolean {
  return accessToken !== null;
}

export async function ensureLoggedIn(): Promise<void> {
  if (!accessToken) {
    throw new TingGoError("NOT_LOGGED_IN",
      "Chưa đăng nhập. Gọi tool tinggo_login trước (ví dụ email: dev@tinggo.local).", 401);
  }
}
