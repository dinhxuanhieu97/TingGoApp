"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, saveTokens } from "@/lib/api";
import type { AuthTokens, Membership } from "@/lib/types";

export default function LoginPage() {
  const router = useRouter();
  const [step, setStep] = useState<"email" | "code">("email");
  const [email, setEmail] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function requestOtp(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      await api("/auth/otp/request", { body: { email } });
      setStep("code");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không gửi được OTP.");
    } finally {
      setLoading(false);
    }
  }

  async function verifyOtp(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const tokens = await api<AuthTokens>("/auth/otp/verify", {
        body: { email, code, deviceName: "merchant-web" },
      });
      saveTokens(tokens.accessToken, tokens.refreshToken);
      const memberships = await api<Membership[]>("/me/memberships");
      router.push(memberships.length === 0 ? "/onboarding" : "/menu");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Xác thực thất bại.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-orange-50 p-4">
      <div className="w-full max-w-sm rounded-2xl bg-white p-8 shadow-lg">
        <h1 className="text-2xl font-bold text-orange-600">TingGo</h1>
        <p className="mb-6 text-sm text-gray-500">Quét bàn, gọi món, quán nhận ngay.</p>

        {step === "email" ? (
          <form onSubmit={requestOtp} className="space-y-4">
            <label className="block">
              <span className="mb-1 block text-sm font-medium">Email chủ quán</span>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="ban@example.com"
                className="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none"
              />
            </label>
            <button
              disabled={loading}
              className="w-full rounded-lg bg-orange-600 py-2 font-semibold text-white hover:bg-orange-700 disabled:opacity-50"
            >
              {loading ? "Đang gửi..." : "Gửi mã OTP"}
            </button>
          </form>
        ) : (
          <form onSubmit={verifyOtp} className="space-y-4">
            <p className="text-sm text-gray-600">
              Mã OTP đã gửi tới <b>{email}</b> (hiệu lực 5 phút).
            </p>
            <input
              required
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="Nhập mã 6 số"
              inputMode="numeric"
              maxLength={6}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-center text-xl tracking-widest focus:border-orange-500 focus:outline-none"
            />
            <button
              disabled={loading}
              className="w-full rounded-lg bg-orange-600 py-2 font-semibold text-white hover:bg-orange-700 disabled:opacity-50"
            >
              {loading ? "Đang xác thực..." : "Đăng nhập"}
            </button>
            <button
              type="button"
              onClick={() => setStep("email")}
              className="w-full text-sm text-gray-500 hover:underline"
            >
              Đổi email
            </button>
          </form>
        )}

        {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
      </div>
    </main>
  );
}
