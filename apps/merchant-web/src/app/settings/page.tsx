"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership } from "@/lib/types";

interface VenueDetail {
  id: string;
  name: string;
  slug: string;
  timezone: string;
  defaultLocale: string;
  currencyCode: string;
  wifiName?: string;
  bankQrImageUrl?: string;
  rowVersion: number;
}

const API_ORIGIN = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1").replace("/api/v1", "");

interface OpeningHourRow {
  dayOfWeek: number;
  openTime: string | null;
  closeTime: string | null;
  isClosed: boolean;
}

const DAY_LABEL = ["", "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật"];

function defaultHours(): OpeningHourRow[] {
  return Array.from({ length: 7 }, (_, i) => ({
    dayOfWeek: i + 1,
    openTime: "07:00",
    closeTime: "22:00",
    isClosed: false,
  }));
}

export default function SettingsPage() {
  const router = useRouter();
  const [venue, setVenue] = useState<VenueDetail | null>(null);
  const [name, setName] = useState("");
  const [wifiName, setWifiName] = useState("");
  const [timezone, setTimezone] = useState("");
  const [locale, setLocale] = useState("");
  const [currency, setCurrency] = useState("");
  const [bankQrFile, setBankQrFile] = useState<File | null>(null);
  const [hours, setHours] = useState<OpeningHourRow[]>(defaultHours());
  const [savingHours, setSavingHours] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (!getTokens()) {
      router.push("/login");
      return;
    }
    (async () => {
      try {
        const memberships = await api<Membership[]>("/me/memberships");
        const venues = await api<VenueDetail[]>(
          `/organizations/${memberships[0].organizationId}/venues`,
        );
        const detail = venues[0];
        if (!detail) return;
        setVenue(detail);
        setName(detail.name);
        setWifiName(detail.wifiName ?? "");
        setTimezone(detail.timezone);
        setLocale(detail.defaultLocale);
        setCurrency(detail.currencyCode);
        const savedHours = await api<OpeningHourRow[]>(`/venues/${detail.id}/opening-hours`);
        if (savedHours.length > 0) {
          setHours(
            Array.from({ length: 7 }, (_, i) =>
              savedHours.find((h) => h.dayOfWeek === i + 1) ?? {
                dayOfWeek: i + 1, openTime: null, closeTime: null, isClosed: true,
              },
            ),
          );
        }
      } catch (err) {
        setError(err instanceof ApiError ? err.message : "Không tải được cài đặt.");
      }
    })();
  }, [router]);

  async function save(e: React.FormEvent) {
    e.preventDefault();
    if (!venue) return;
    setSaving(true);
    setError("");
    try {
      let bankQrImageUrl: string | undefined;
      if (bankQrFile) {
        const formData = new FormData();
        formData.append("file", bankQrFile);
        const uploaded = await api<{ url: string }>("/files/images", { formData });
        bankQrImageUrl = uploaded.url;
      }
      const updated = await api<VenueDetail>(`/venues/${venue.id}`, {
        method: "PATCH",
        body: {
          name: name.trim(),
          wifiName: wifiName.trim() || null,
          timezone,
          defaultLocale: locale,
          currencyCode: currency,
          bankQrImageUrl,
          rowVersion: venue.rowVersion,
        },
      });
      setVenue(updated);
      setBankQrFile(null);
      setNotice("Đã lưu cài đặt.");
      setTimeout(() => setNotice(""), 3000);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không lưu được.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <main className="min-h-screen bg-orange-50">
      <header className="flex items-center justify-between border-b bg-white px-6 py-3">
        <div className="flex items-center gap-4">
          <span className="text-xl font-bold text-orange-600">TingGo</span>
          <nav className="flex gap-3 text-sm">
            <a href="/menu" className="text-gray-500 hover:text-orange-600">Menu</a>
            <a href="/tables" className="text-gray-500 hover:text-orange-600">Bàn & QR</a>
            <a href="/orders" className="text-gray-500 hover:text-orange-600">Order</a>
            <a href="/reports" className="text-gray-500 hover:text-orange-600">Báo cáo</a>
            <a href="/staff" className="text-gray-500 hover:text-orange-600">Nhân viên</a>
            <span className="font-semibold text-orange-600">Cài đặt</span>
          </nav>
        </div>
        <span className="text-sm text-gray-500">{venue?.name}</span>
      </header>

      <div className="mx-auto max-w-xl p-6">
        {error && <p className="mb-3 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">{error}</p>}
        {notice && <p className="mb-3 rounded-lg bg-green-100 px-4 py-2 text-sm text-green-800">{notice}</p>}

        <form onSubmit={save} className="space-y-4 rounded-2xl bg-white p-5 shadow">
          <h2 className="font-semibold">Cài đặt quán</h2>
          <label className="block text-sm">
            <span className="font-medium">Tên quán</span>
            <input required value={name} onChange={(e) => setName(e.target.value)} maxLength={200}
              className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none" />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Tên Wi-Fi (hiển thị cho khách)</span>
            <input value={wifiName} onChange={(e) => setWifiName(e.target.value)} maxLength={200}
              className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none" />
          </label>
          <div className="grid grid-cols-3 gap-2">
            <label className="block text-sm">
              <span className="font-medium">Múi giờ</span>
              <input value={timezone} onChange={(e) => setTimezone(e.target.value)}
                className="mt-1 w-full rounded-lg border border-gray-300 px-2 py-2 text-xs" />
            </label>
            <label className="block text-sm">
              <span className="font-medium">Ngôn ngữ</span>
              <input value={locale} onChange={(e) => setLocale(e.target.value)}
                className="mt-1 w-full rounded-lg border border-gray-300 px-2 py-2 text-xs" />
            </label>
            <label className="block text-sm">
              <span className="font-medium">Tiền tệ</span>
              <input value={currency} onChange={(e) => setCurrency(e.target.value)} maxLength={3}
                className="mt-1 w-full rounded-lg border border-gray-300 px-2 py-2 text-xs uppercase" />
            </label>
          </div>

          <div className="rounded-xl bg-orange-50 p-3">
            <p className="text-sm font-medium">QR ngân hàng (chuyển khoản tĩnh — VietQR)</p>
            <p className="mb-2 text-xs text-gray-500">
              Khách bấm &quot;Thanh toán&quot; sẽ thấy ảnh QR này để chuyển khoản. Nhân viên đối chiếu rồi xác nhận.
            </p>
            {venue?.bankQrImageUrl && (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={`${API_ORIGIN}${venue.bankQrImageUrl}`} alt="QR ngân hàng"
                className="mb-2 h-36 w-36 rounded-lg border object-contain" />
            )}
            <input type="file" accept="image/jpeg,image/png,image/webp"
              onChange={(e) => setBankQrFile(e.target.files?.[0] ?? null)}
              className="block w-full text-xs" />
          </div>

          <button disabled={saving}
            className="w-full rounded-xl bg-orange-600 py-2.5 font-semibold text-white hover:bg-orange-700 disabled:opacity-50">
            {saving ? "Đang lưu..." : "Lưu cài đặt"}
          </button>
        </form>

        <div className="mt-6 rounded-2xl bg-white p-5 shadow">
          <h2 className="mb-3 font-semibold">Giờ mở cửa</h2>
          <div className="space-y-1.5">
            {hours.map((row, index) => (
              <div key={row.dayOfWeek} className="flex items-center gap-2 text-sm">
                <span className="w-16 font-medium">{DAY_LABEL[row.dayOfWeek]}</span>
                <input
                  type="time"
                  value={row.openTime ?? ""}
                  disabled={row.isClosed}
                  onChange={(e) =>
                    setHours((prev) => prev.map((h, i) => (i === index ? { ...h, openTime: e.target.value } : h)))
                  }
                  className="rounded-lg border border-gray-300 px-2 py-1 disabled:bg-gray-100 disabled:text-gray-400"
                />
                <span className="text-gray-400">–</span>
                <input
                  type="time"
                  value={row.closeTime ?? ""}
                  disabled={row.isClosed}
                  onChange={(e) =>
                    setHours((prev) => prev.map((h, i) => (i === index ? { ...h, closeTime: e.target.value } : h)))
                  }
                  className="rounded-lg border border-gray-300 px-2 py-1 disabled:bg-gray-100 disabled:text-gray-400"
                />
                <label className="ml-auto flex items-center gap-1 text-xs text-gray-500">
                  <input
                    type="checkbox"
                    checked={row.isClosed}
                    onChange={(e) =>
                      setHours((prev) => prev.map((h, i) => (i === index ? { ...h, isClosed: e.target.checked } : h)))
                    }
                  />
                  Nghỉ
                </label>
              </div>
            ))}
          </div>
          <button
            disabled={savingHours}
            onClick={async () => {
              if (!venue) return;
              setSavingHours(true);
              setError("");
              try {
                await api(`/venues/${venue.id}/opening-hours`, {
                  method: "PUT",
                  body: { days: hours },
                });
                setNotice("Đã lưu giờ mở cửa.");
                setTimeout(() => setNotice(""), 3000);
              } catch (err) {
                setError(err instanceof ApiError ? err.message : "Không lưu được giờ mở cửa.");
              } finally {
                setSavingHours(false);
              }
            }}
            className="mt-3 w-full rounded-xl bg-orange-600 py-2.5 font-semibold text-white hover:bg-orange-700 disabled:opacity-50"
          >
            {savingHours ? "Đang lưu..." : "Lưu giờ mở cửa"}
          </button>
        </div>
      </div>
    </main>
  );
}
