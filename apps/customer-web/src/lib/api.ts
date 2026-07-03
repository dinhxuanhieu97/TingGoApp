const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";
export const API_ORIGIN = API_URL.replace("/api/v1", "");

export class ApiError extends Error {
  constructor(
    public code: string,
    message: string,
    public status: number,
  ) {
    super(message);
  }
}

export async function publicApi<T = unknown>(
  path: string,
  options: { method?: string; body?: unknown; headers?: Record<string, string> } = {},
): Promise<T> {
  const res = await fetch(`${API_URL}${path}`, {
    method: options.method ?? (options.body !== undefined ? "POST" : "GET"),
    headers: {
      ...(options.body !== undefined ? { "Content-Type": "application/json" } : {}),
      ...options.headers,
    },
    body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : undefined;
  if (!res.ok) {
    throw new ApiError(data?.code ?? "UNKNOWN", data?.message ?? "Đã xảy ra lỗi.", res.status);
  }
  return data as T;
}

export function formatMoney(minor: number, currency: string, locale = "vi-VN"): string {
  return new Intl.NumberFormat(locale, { style: "currency", currency }).format(
    currency === "VND" ? minor : minor / 100,
  );
}
