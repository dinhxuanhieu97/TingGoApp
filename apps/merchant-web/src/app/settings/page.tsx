"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership } from "@/lib/types";
import MerchantNav from "@/components/MerchantNav";

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

// Lựa chọn phổ biến — vẫn cho phép giá trị khác đã lưu trong DB
const TIMEZONES = [
  { value: "Asia/Ho_Chi_Minh", label: "Việt Nam (GMT+7)" },
  { value: "Asia/Bangkok", label: "Bangkok (GMT+7)" },
  { value: "Asia/Singapore", label: "Singapore (GMT+8)" },
  { value: "Asia/Tokyo", label: "Tokyo (GMT+9)" },
];
const LOCALES = [
  { value: "vi-VN", label: "Tiếng Việt" },
  { value: "en-US", label: "English" },
  { value: "zh-CN", label: "中文" },
  { value: "ja-JP", label: "日本語" },
];
const CURRENCIES = [
  { value: "VND", label: "VND — Việt Nam Đồng" },
  { value: "USD", label: "USD — US Dollar" },
  { value: "JPY", label: "JPY — Japanese Yen" },
  { value: "CNY", label: "CNY — Chinese Yuan" },
];

function defaultHours(): OpeningHourRow[] {
  return Array.from({ length: 7 }, (_, i) => ({
    dayOfWeek: i + 1,
    openTime: "07:00",
    closeTime: "22:00",
    isClosed: false,
  }));
}

const inputClass =
  "mt-1 w-full rounded-xl border border-gray-300 px-3 py-2.5 text-sm focus:border-brand-500 focus:outline-none focus:ring-2 focus:ring-brand-100";

type SectionIcon = "store" | "card" | "clock";

const ICON_PATHS: Record<SectionIcon, React.ReactNode> = {
  store: (
    <>
      <path d="M3 9l1.5-5h15L21 9" />
      <path d="M4 9v11a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V9" />
      <path d="M3 9h18" />
      <path d="M9 21v-6h6v6" />
    </>
  ),
  card: (
    <>
      <rect x="2" y="5" width="20" height="14" rx="2" />
      <line x1="2" y1="10" x2="22" y2="10" />
      <line x1="6" y1="15" x2="10" y2="15" />
    </>
  ),
  clock: (
    <>
      <circle cx="12" cy="12" r="9" />
      <polyline points="12 7 12 12 15.5 14" />
    </>
  ),
};

interface SectionCardProps {
  icon: SectionIcon;
  title: string;
  description: string;
  children: React.ReactNode;
}

function SectionCard({ icon, title, description, children }: SectionCardProps) {
  return (
    <section className="rounded-2xl border border-brand-100/60 bg-white p-4 shadow-sm sm:p-6">
      <div className="mb-5 flex items-center gap-3">
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-100 text-brand-600">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
            strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
            {ICON_PATHS[icon]}
          </svg>
        </span>
        <div>
          <h2 className="font-semibold leading-tight">{title}</h2>
          <p className="mt-0.5 text-xs text-gray-500">{description}</p>
        </div>
      </div>
      {children}
    </section>
  );
}

interface ToggleProps {
  checked: boolean;
  onChange: (checked: boolean) => void;
  label: string;
}

/** Toggle switch chuẩn 44px touch target */
function Toggle({ checked, onChange, label }: ToggleProps) {
  return (
    <button
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={`relative h-6 w-11 shrink-0 rounded-full transition-colors ${
        checked ? "bg-brand-600" : "bg-gray-300"
      }`}
    >
      <span
        className={`absolute top-0.5 h-5 w-5 rounded-full bg-white shadow transition-transform ${
          checked ? "translate-x-[22px]" : "translate-x-0.5"
        }`}
      />
    </button>
  );
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
  const [bankQrPreview, setBankQrPreview] = useState<string | null>(null);
  const [hours, setHours] = useState<OpeningHourRow[]>(defaultHours());
  const [savingHours, setSavingHours] = useState(false);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);

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

  function pickBankQr(file: File | null) {
    setBankQrFile(file);
    if (bankQrPreview) URL.revokeObjectURL(bankQrPreview);
    setBankQrPreview(file ? URL.createObjectURL(file) : null);
  }

  function showNotice(message: string) {
    setNotice(message);
    setTimeout(() => setNotice(""), 3000);
  }

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
      pickBankQr(null);
      showNotice("Đã lưu cài đặt ✓");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không lưu được.");
    } finally {
      setSaving(false);
    }
  }

  async function saveHours() {
    if (!venue) return;
    setSavingHours(true);
    setError("");
    try {
      await api(`/venues/${venue.id}/opening-hours`, {
        method: "PUT",
        body: { days: hours },
      });
      showNotice("Đã lưu giờ mở cửa ✓");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không lưu được giờ mở cửa.");
    } finally {
      setSavingHours(false);
    }
  }

  /** Áp giờ Thứ 2 cho tất cả các ngày đang mở */
  function applyMondayToAll() {
    const monday = hours[0];
    setHours((prev) =>
      prev.map((h) =>
        h.isClosed ? h : { ...h, openTime: monday.openTime, closeTime: monday.closeTime },
      ),
    );
  }

  function updateHour(index: number, patch: Partial<OpeningHourRow>) {
    setHours((prev) => prev.map((h, i) => (i === index ? { ...h, ...patch } : h)));
  }

  const selectValue = (options: { value: string }[], current: string) =>
    options.some((o) => o.value === current) ? current : "";

  return (
    <main className="min-h-screen bg-orange-50">
      <MerchantNav venueName={venue?.name} />

      {/* Toast thông báo nổi */}
      {notice && (
        <div className="fixed left-1/2 top-16 z-30 -translate-x-1/2 rounded-full bg-gray-900/90 px-4 py-2 text-sm font-medium text-white shadow-lg">
          {notice}
        </div>
      )}

      <div className="mx-auto max-w-4xl p-3 sm:p-6">
        <div className="mb-4">
          <h1 className="text-lg font-bold">Cài đặt quán</h1>
          <p className="text-xs text-gray-500">
            Thông tin ở đây hiển thị trực tiếp cho khách khi quét QR.
          </p>
        </div>

        {error && (
          <p className="mb-3 rounded-xl bg-danger-bg px-4 py-2.5 text-sm text-danger">
            {error}
            <button onClick={() => setError("")} className="ml-2 font-bold">×</button>
          </p>
        )}

        <div className="grid items-start gap-4 lg:grid-cols-2">
          <div className="space-y-4">
            <form onSubmit={save} className="contents">
              <SectionCard
                icon="store"
                title="Thông tin quán"
                description="Tên, Wi-Fi và định dạng hiển thị cho khách"
              >
                <div className="space-y-3">
                  <label className="block text-sm">
                    <span className="font-medium">Tên quán</span>
                    <input required value={name} onChange={(e) => setName(e.target.value)}
                      maxLength={200} className={inputClass} />
                  </label>
                  <label className="block text-sm">
                    <span className="font-medium">Tên Wi-Fi</span>
                    <span className="ml-1 text-xs font-normal text-gray-400">
                      (khách thấy ngay dưới tên quán)
                    </span>
                    <input value={wifiName} onChange={(e) => setWifiName(e.target.value)}
                      maxLength={200} placeholder="VD: CafeHieu_5G" className={inputClass} />
                  </label>
                  <div className="grid gap-3 sm:grid-cols-3">
                    <label className="block text-sm">
                      <span className="font-medium">Múi giờ</span>
                      <select value={selectValue(TIMEZONES, timezone)}
                        onChange={(e) => setTimezone(e.target.value)} className={inputClass}>
                        {!selectValue(TIMEZONES, timezone) && (
                          <option value="">{timezone}</option>
                        )}
                        {TIMEZONES.map((tz) => (
                          <option key={tz.value} value={tz.value}>{tz.label}</option>
                        ))}
                      </select>
                    </label>
                    <label className="block text-sm">
                      <span className="font-medium">Ngôn ngữ</span>
                      <select value={selectValue(LOCALES, locale)}
                        onChange={(e) => setLocale(e.target.value)} className={inputClass}>
                        {!selectValue(LOCALES, locale) && <option value="">{locale}</option>}
                        {LOCALES.map((l) => (
                          <option key={l.value} value={l.value}>{l.label}</option>
                        ))}
                      </select>
                    </label>
                    <label className="block text-sm">
                      <span className="font-medium">Tiền tệ</span>
                      <select value={selectValue(CURRENCIES, currency)}
                        onChange={(e) => setCurrency(e.target.value)} className={inputClass}>
                        {!selectValue(CURRENCIES, currency) && (
                          <option value="">{currency}</option>
                        )}
                        {CURRENCIES.map((c) => (
                          <option key={c.value} value={c.value}>{c.label}</option>
                        ))}
                      </select>
                    </label>
                  </div>
                </div>
              </SectionCard>

              <SectionCard
                icon="card"
                title="QR chuyển khoản"
                description="Khách bấm “Thanh toán” sẽ thấy ảnh QR này (VietQR tĩnh)"
              >
                <div className="flex items-start gap-4">
                  {(bankQrPreview || venue?.bankQrImageUrl) ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={bankQrPreview ?? `${API_ORIGIN}${venue?.bankQrImageUrl}`}
                      alt="QR ngân hàng"
                      className="h-28 w-28 shrink-0 rounded-xl border object-contain"
                    />
                  ) : (
                    <div className="flex h-28 w-28 shrink-0 items-center justify-center rounded-xl border-2 border-dashed border-gray-300 text-3xl text-gray-300">
                      ⬚
                    </div>
                  )}
                  <div className="flex-1">
                    <input
                      ref={fileInputRef}
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      onChange={(e) => pickBankQr(e.target.files?.[0] ?? null)}
                      className="hidden"
                    />
                    <button
                      type="button"
                      onClick={() => fileInputRef.current?.click()}
                      className="rounded-xl border border-brand-300 px-4 py-2 text-sm font-semibold text-brand-600 hover:bg-brand-50"
                    >
                      {venue?.bankQrImageUrl || bankQrPreview ? "Đổi ảnh QR" : "Tải ảnh QR lên"}
                    </button>
                    {bankQrFile && (
                      <p className="mt-1.5 text-xs text-gray-500">
                        Đã chọn: {bankQrFile.name} — bấm <b>Lưu cài đặt</b> để áp dụng.
                      </p>
                    )}
                    <p className="mt-1.5 text-xs text-gray-400">
                      Xuất ảnh VietQR từ app ngân hàng của bạn. Nhân viên đối chiếu tiền vào
                      tài khoản rồi xác nhận đơn.
                    </p>
                  </div>
                </div>
              </SectionCard>

              <button
                disabled={saving}
                className="w-full rounded-xl bg-brand-600 py-3 font-semibold text-white shadow-sm hover:bg-brand-700 disabled:opacity-50"
              >
                {saving ? "Đang lưu..." : "Lưu cài đặt"}
              </button>
            </form>
          </div>

          <SectionCard
            icon="clock"
            title="Giờ mở cửa"
            description="Khách thấy “Đang mở cửa / Ngoài giờ” trên trang gọi món"
          >
            <button
              type="button"
              onClick={applyMondayToAll}
              className="mb-3 rounded-full border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-600 hover:bg-gray-50"
            >
              ⧉ Áp dụng giờ Thứ 2 cho cả tuần
            </button>
            <div className="divide-y">
              {hours.map((row, index) => (
                <div key={row.dayOfWeek} className="flex flex-wrap items-center gap-2 py-2.5 text-sm">
                  <span className="w-16 font-medium">{DAY_LABEL[row.dayOfWeek]}</span>
                  {row.isClosed ? (
                    <span className="flex-1 text-xs text-gray-400">Nghỉ cả ngày</span>
                  ) : (
                    <div className="flex flex-1 items-center gap-1.5">
                      <input
                        type="time"
                        value={row.openTime ?? ""}
                        onChange={(e) => updateHour(index, { openTime: e.target.value })}
                        className="rounded-lg border border-gray-300 px-2 py-1.5 text-sm focus:border-brand-500 focus:outline-none"
                      />
                      <span className="text-gray-400">–</span>
                      <input
                        type="time"
                        value={row.closeTime ?? ""}
                        onChange={(e) => updateHour(index, { closeTime: e.target.value })}
                        className="rounded-lg border border-gray-300 px-2 py-1.5 text-sm focus:border-brand-500 focus:outline-none"
                      />
                    </div>
                  )}
                  <div className="ml-auto flex items-center gap-1.5">
                    <span className="text-xs text-gray-400">
                      {row.isClosed ? "Nghỉ" : "Mở"}
                    </span>
                    <Toggle
                      checked={!row.isClosed}
                      onChange={(open) =>
                        updateHour(index, {
                          isClosed: !open,
                          ...(open && !row.openTime
                            ? { openTime: "07:00", closeTime: "22:00" }
                            : {}),
                        })
                      }
                      label={`${DAY_LABEL[row.dayOfWeek]} ${row.isClosed ? "đang nghỉ" : "đang mở"}`}
                    />
                  </div>
                </div>
              ))}
            </div>
            <button
              disabled={savingHours}
              onClick={saveHours}
              className="mt-3 w-full rounded-xl bg-brand-600 py-3 font-semibold text-white shadow-sm hover:bg-brand-700 disabled:opacity-50"
            >
              {savingHours ? "Đang lưu..." : "Lưu giờ mở cửa"}
            </button>
          </SectionCard>
        </div>
      </div>
    </main>
  );
}
