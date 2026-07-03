"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError } from "@/lib/api";

interface OrgDto {
  id: string;
}

export default function OnboardingPage() {
  const router = useRouter();
  const [orgName, setOrgName] = useState("");
  const [venueName, setVenueName] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setLoading(true);
    try {
      const org = await api<OrgDto>("/organizations", { body: { name: orgName } });
      await api(`/organizations/${org.id}/venues`, { body: { name: venueName } });
      router.push("/menu");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không tạo được quán.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-orange-50 p-4">
      <div className="w-full max-w-md rounded-2xl bg-white p-8 shadow-lg">
        <h1 className="text-xl font-bold">Tạo quán của bạn</h1>
        <p className="mb-6 text-sm text-gray-500">Chỉ mất một phút — bạn có thể sửa sau.</p>
        <form onSubmit={submit} className="space-y-4">
          <label className="block">
            <span className="mb-1 block text-sm font-medium">Tên tổ chức / thương hiệu</span>
            <input
              required
              maxLength={200}
              value={orgName}
              onChange={(e) => setOrgName(e.target.value)}
              placeholder="VD: Cà Phê Hiếu"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none"
            />
          </label>
          <label className="block">
            <span className="mb-1 block text-sm font-medium">Tên quán (chi nhánh đầu tiên)</span>
            <input
              required
              maxLength={200}
              value={venueName}
              onChange={(e) => setVenueName(e.target.value)}
              placeholder="VD: Cà Phê Hiếu — Quận 1"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none"
            />
          </label>
          <button
            disabled={loading}
            className="w-full rounded-lg bg-orange-600 py-2 font-semibold text-white hover:bg-orange-700 disabled:opacity-50"
          >
            {loading ? "Đang tạo..." : "Tạo quán"}
          </button>
        </form>
        {error && <p className="mt-4 text-sm text-red-600">{error}</p>}
      </div>
    </main>
  );
}
