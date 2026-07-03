"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import QRCode from "qrcode";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership, Venue } from "@/lib/types";

interface Area {
  id: string;
  venueId: string;
  name: string;
  sortOrder: number;
}

interface DiningTable {
  id: string;
  areaId: string;
  code: string;
  name: string;
  status: string;
  hasActiveQr: boolean;
}

interface QrInfo {
  code: string;
  qrUrl: string;
  dataUrl: string;
}

export default function TablesPage() {
  const router = useRouter();
  const [venue, setVenue] = useState<Venue | null>(null);
  const [areas, setAreas] = useState<Area[]>([]);
  const [tables, setTables] = useState<DiningTable[]>([]);
  const [qrModal, setQrModal] = useState<QrInfo | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

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
        const venues = await api<Venue[]>(
          `/organizations/${memberships[0].organizationId}/venues`,
        );
        setVenue(venues[0] ?? null);
      } catch (err) {
        showError(err);
      } finally {
        setLoading(false);
      }
    })();
  }, [router]);

  const reload = useCallback(async (v: Venue) => {
    const [areaItems, tableItems] = await Promise.all([
      api<Area[]>(`/venues/${v.id}/areas`),
      api<DiningTable[]>(`/venues/${v.id}/tables`),
    ]);
    setAreas(areaItems);
    setTables(tableItems);
  }, []);

  useEffect(() => {
    if (venue) reload(venue).catch(showError);
  }, [venue, reload]);

  async function addArea(name: string) {
    if (!venue) return;
    try {
      await api(`/venues/${venue.id}/areas`, { body: { name } });
      await reload(venue);
    } catch (err) {
      showError(err);
    }
  }

  async function bulkCreate(areaId: string, count: number) {
    if (!venue) return;
    try {
      await api(`/venues/${venue.id}/tables/bulk`, { body: { areaId, count } });
      await reload(venue);
    } catch (err) {
      showError(err);
    }
  }

  async function showQr(table: DiningTable) {
    try {
      const result = await api<{ code: string; qrUrl: string }>(
        `/tables/${table.id}/qr/regenerate`,
        { method: "POST" },
      );
      const dataUrl = await QRCode.toDataURL(result.qrUrl, { width: 320, margin: 2 });
      setQrModal({ code: table.code, qrUrl: result.qrUrl, dataUrl });
      if (venue) await reload(venue);
    } catch (err) {
      showError(err);
    }
  }

  function printQr(qr: QrInfo) {
    const win = window.open("", "_blank");
    if (!win) return;
    win.document.write(`<!doctype html><html><head><title>QR ${qr.code}</title></head>
      <body style="display:flex;flex-direction:column;align-items:center;font-family:sans-serif;padding:40px">
      <h1 style="color:#ea580c">TingGo</h1>
      <h2>Bàn ${qr.code}</h2>
      <img src="${qr.dataUrl}" width="320" height="320"/>
      <p>Quét mã để xem menu và gọi món</p>
      <script>window.onload = () => window.print()</script>
      </body></html>`);
    win.document.close();
  }

  if (loading) return <main className="p-8 text-gray-500">Đang tải...</main>;

  return (
    <main className="min-h-screen bg-orange-50">
      <header className="flex items-center justify-between border-b bg-white px-6 py-3">
        <div className="flex items-center gap-4">
          <span className="text-xl font-bold text-orange-600">TingGo</span>
          <nav className="flex gap-3 text-sm">
            <a href="/menu" className="text-gray-500 hover:text-orange-600">
              Menu
            </a>
            <span className="font-semibold text-orange-600">Bàn & QR</span>
          </nav>
        </div>
        <span className="text-sm text-gray-500">{venue?.name}</span>
      </header>

      {error && (
        <div className="mx-6 mt-4 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">
            ×
          </button>
        </div>
      )}

      <div className="p-6">
        <section className="mb-6 rounded-2xl bg-white p-4 shadow">
          <h2 className="mb-3 font-semibold">Khu vực</h2>
          <div className="flex flex-wrap items-center gap-2">
            {areas.map((a) => (
              <span key={a.id} className="rounded-full bg-orange-100 px-3 py-1 text-sm">
                {a.name}
              </span>
            ))}
            <AreaForm onAdd={addArea} />
          </div>
          {areas.length > 0 && (
            <div className="mt-3 flex items-center gap-2 text-sm">
              <span className="text-gray-500">Tạo nhanh:</span>
              {[5, 10].map((count) => (
                <button
                  key={count}
                  onClick={() => bulkCreate(areas[0].id, count)}
                  className="rounded-lg border border-orange-300 px-3 py-1 text-orange-600 hover:bg-orange-50"
                >
                  +{count} bàn
                </button>
              ))}
            </div>
          )}
        </section>

        <section className="rounded-2xl bg-white p-4 shadow">
          <h2 className="mb-3 font-semibold">Bàn ({tables.length})</h2>
          <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {tables.map((t) => (
              <li key={t.id} className="rounded-xl border p-3">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="font-semibold">{t.code}</p>
                    <p className="text-xs text-gray-500">{t.name}</p>
                  </div>
                  <span
                    className={`rounded-full px-2 py-0.5 text-xs ${
                      t.status === "active"
                        ? "bg-green-100 text-green-700"
                        : "bg-gray-200 text-gray-500"
                    }`}
                  >
                    {t.status === "active" ? "Hoạt động" : "Khóa"}
                  </span>
                </div>
                <button
                  onClick={() => showQr(t)}
                  className="mt-2 w-full rounded-lg bg-orange-600 py-1.5 text-xs font-semibold text-white hover:bg-orange-700"
                >
                  {t.hasActiveQr ? "In lại QR (tạo mã mới)" : "Tạo QR"}
                </button>
              </li>
            ))}
          </ul>
          {tables.length === 0 && (
            <p className="py-8 text-center text-sm text-gray-400">
              Chưa có bàn. Tạo khu vực rồi dùng nút &quot;Tạo nhanh&quot; phía trên.
            </p>
          )}
        </section>
      </div>

      {qrModal && (
        <div
          className="fixed inset-0 flex items-center justify-center bg-black/40 p-4"
          onClick={() => setQrModal(null)}
        >
          <div
            className="w-full max-w-xs rounded-2xl bg-white p-6 text-center shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-lg font-bold">Bàn {qrModal.code}</h3>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={qrModal.dataUrl} alt="QR" className="mx-auto my-3" />
            <p className="break-all text-xs text-gray-400">{qrModal.qrUrl}</p>
            <div className="mt-4 flex gap-2">
              <button
                onClick={() => printQr(qrModal)}
                className="flex-1 rounded-lg bg-orange-600 py-2 text-sm font-semibold text-white hover:bg-orange-700"
              >
                In poster
              </button>
              <button
                onClick={() => setQrModal(null)}
                className="flex-1 rounded-lg border py-2 text-sm hover:bg-gray-50"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </main>
  );
}

function AreaForm({ onAdd }: { onAdd: (name: string) => Promise<void> }) {
  const [name, setName] = useState("");
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        if (!name.trim()) return;
        await onAdd(name.trim());
        setName("");
      }}
      className="flex gap-1"
    >
      <input
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Thêm khu vực..."
        maxLength={100}
        className="rounded-lg border border-gray-300 px-2 py-1 text-sm focus:border-orange-500 focus:outline-none"
      />
      <button className="rounded-lg bg-orange-600 px-2 text-sm font-semibold text-white hover:bg-orange-700">
        +
      </button>
    </form>
  );
}
