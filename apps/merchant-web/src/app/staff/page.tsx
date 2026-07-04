"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership, Venue } from "@/lib/types";
import MerchantNav from "@/components/MerchantNav";

interface StaffMember {
  id: string;
  userId: string;
  displayName: string;
  role: string;
  staffCode?: string;
  status: string;
}

const ROLE_LABEL: Record<string, string> = {
  owner: "Chủ quán",
  manager: "Quản lý",
  cashier: "Thu ngân",
  waiter: "Phục vụ",
  kitchen: "Bếp",
};

export default function StaffPage() {
  const router = useRouter();
  const [venue, setVenue] = useState<Venue | null>(null);
  const [staff, setStaff] = useState<StaffMember[]>([]);
  const [name, setName] = useState("");
  const [role, setRole] = useState("waiter");
  const [pin, setPin] = useState("");
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

  const load = useCallback(async (v: Venue) => {
    setStaff(await api<StaffMember[]>(`/venues/${v.id}/staff`));
  }, []);

  useEffect(() => {
    if (!getTokens()) {
      router.push("/login");
      return;
    }
    (async () => {
      try {
        const memberships = await api<Membership[]>("/me/memberships");
        if (memberships.length === 0) {
          router.push("/onboarding");
          return;
        }
        const venues = await api<Venue[]>(`/organizations/${memberships[0].organizationId}/venues`);
        if (venues[0]) {
          setVenue(venues[0]);
          await load(venues[0]);
        }
      } catch (err) {
        showError(err);
      }
    })();
  }, [router, load]);

  async function createStaff(e: React.FormEvent) {
    e.preventDefault();
    if (!venue) return;
    setError("");
    try {
      const created = await api<{ staffCode: string }>(`/venues/${venue.id}/staff`, {
        body: { displayName: name.trim(), role, pin },
      });
      setNotice(`Đã tạo — mã đăng nhập: ${created.staffCode}, PIN: ${pin} (đưa cho nhân viên)`);
      setName("");
      setPin("");
      await load(venue);
    } catch (err) {
      showError(err);
    }
  }

  async function resetPin(member: StaffMember) {
    if (!venue) return;
    const newPin = prompt(`PIN mới cho ${member.displayName} (4–6 số):`);
    if (!newPin) return;
    try {
      await api(`/venues/${venue.id}/staff/${member.id}/reset-pin`, { body: { pin: newPin } });
      setNotice(`Đã đặt lại PIN cho ${member.displayName}: ${newPin}`);
    } catch (err) {
      showError(err);
    }
  }

  async function toggleStatus(member: StaffMember) {
    if (!venue) return;
    const action = member.status === "active" ? "revoke" : "activate";
    try {
      await api(`/venues/${venue.id}/staff/${member.id}/${action}`, { method: "POST" });
      await load(venue);
    } catch (err) {
      showError(err);
    }
  }

  return (
    <main className="min-h-screen bg-orange-50">
      <MerchantNav venueName={venue?.name} />

      <div className="p-3 sm:p-6">
        {error && (
          <div className="mb-4 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
            {error} <button onClick={() => setError("")} className="ml-1 font-bold">×</button>
          </div>
        )}
        {notice && (
          <div className="mb-4 rounded-lg bg-green-100 px-4 py-2 text-sm text-green-800">
            {notice} <button onClick={() => setNotice("")} className="ml-1 font-bold">×</button>
          </div>
        )}

        <section className="mb-6 rounded-2xl bg-white p-4 shadow">
          <h2 className="mb-3 font-semibold">Thêm nhân viên</h2>
          <form onSubmit={createStaff} className="grid gap-2 md:grid-cols-4">
            <input required value={name} onChange={(e) => setName(e.target.value)}
              placeholder="Tên nhân viên" maxLength={200}
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-orange-500 focus:outline-none" />
            <select value={role} onChange={(e) => setRole(e.target.value)}
              className="rounded-lg border border-gray-300 px-2 py-2 text-sm">
              <option value="waiter">Phục vụ</option>
              <option value="cashier">Thu ngân</option>
              <option value="kitchen">Bếp</option>
              <option value="manager">Quản lý</option>
            </select>
            <input required value={pin} onChange={(e) => setPin(e.target.value)}
              placeholder="PIN (4–6 số)" inputMode="numeric" maxLength={6}
              className="rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-orange-500 focus:outline-none" />
            <button className="rounded-lg bg-orange-600 px-4 py-2 text-sm font-semibold text-white hover:bg-orange-700">
              Thêm
            </button>
          </form>
          <p className="mt-2 text-xs text-gray-400">
            Nhân viên đăng nhập app bằng: <b>Mã quán</b>{" "}
            <code className="rounded bg-orange-100 px-1.5 py-0.5 text-sm font-bold text-orange-700">
              {venue?.joinCode ?? "..."}
            </code>{" "}
            + mã nhân viên (NVxx) + PIN.
          </p>
        </section>

        <section className="rounded-2xl bg-white p-4 shadow">
          <h2 className="mb-3 font-semibold">Danh sách ({staff.length})</h2>
          <ul className="divide-y">
            {staff.map((member) => (
              <li key={member.id} className="flex items-center gap-3 py-2.5">
                <div className="flex-1">
                  <p className={`text-sm font-medium ${member.status !== "active" ? "text-gray-400 line-through" : ""}`}>
                    {member.displayName}
                  </p>
                  <p className="text-xs text-gray-500">
                    {ROLE_LABEL[member.role] ?? member.role}
                    {member.staffCode && <> · Mã: <b>{member.staffCode}</b></>}
                  </p>
                </div>
                <span className={`rounded-full px-2 py-0.5 text-xs ${
                  member.status === "active" ? "bg-green-100 text-green-700" : "bg-gray-200 text-gray-500"
                }`}>
                  {member.status === "active" ? "Hoạt động" : "Đã thu hồi"}
                </span>
                {member.role !== "owner" && (
                  <>
                    <button onClick={() => resetPin(member)}
                      className="rounded-lg border border-gray-300 px-2.5 py-1 text-xs hover:bg-gray-50">
                      Đặt lại PIN
                    </button>
                    <button onClick={() => toggleStatus(member)}
                      className={`rounded-lg px-2.5 py-1 text-xs font-semibold text-white ${
                        member.status === "active" ? "bg-red-500 hover:bg-red-600" : "bg-green-600 hover:bg-green-700"
                      }`}>
                      {member.status === "active" ? "Thu hồi" : "Kích hoạt"}
                    </button>
                  </>
                )}
              </li>
            ))}
          </ul>
          {staff.length === 0 && <p className="py-6 text-center text-sm text-gray-400">Chưa có nhân viên.</p>}
        </section>
      </div>
    </main>
  );
}
