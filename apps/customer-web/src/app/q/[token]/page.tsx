"use client";

import { use, useEffect, useMemo, useRef, useState } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { publicApi, ApiError, formatMoney, API_ORIGIN } from "@/lib/api";
import type {
  CartItem,
  PublicMenu,
  PublicProduct,
  QrContext,
} from "@/lib/types";
import { cartTotal } from "@/lib/types";
import {
  LANGS,
  getInitialLang,
  normalizeSearch,
  saveLang,
  t,
  type Lang,
  type MsgKey,
} from "@/lib/i18n";

interface SessionOrders {
  status: string;
  totalMinor: number;
  orders: {
    id: string;
    orderNumber: string;
    status: string;
    totalMinor: number;
    items: { productName: string; quantity: number }[];
  }[];
}

const KNOWN_STATUSES = new Set([
  "submitted", "confirmed", "preparing", "ready", "completed", "rejected", "cancelled",
]);

function statusLabel(lang: Lang, status: string): string {
  return KNOWN_STATUSES.has(status) ? t(lang, `status_${status}` as MsgKey) : status;
}

export default function QrPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const [context, setContext] = useState<QrContext | null>(null);
  const [menu, setMenu] = useState<PublicMenu | null>(null);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [selected, setSelected] = useState<PublicProduct | null>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [sessionOrders, setSessionOrders] = useState<SessionOrders | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [lang, setLang] = useState<Lang>("vi");

  useEffect(() => {
    setLang(getInitialLang());
  }, []);

  function changeLang(next: Lang) {
    setLang(next);
    saveLang(next);
  }

  useEffect(() => {
    (async () => {
      try {
        const qrContext = await publicApi<QrContext>(`/public/q/${token}`);
        setContext(qrContext);
        const session = await publicApi<{ sessionToken: string }>("/public/table-sessions", {
          body: { qrToken: token },
        });
        setSessionToken(session.sessionToken);
      } catch (err) {
        setError(err instanceof ApiError ? err.message : t("vi", "menuLoadError"));
      } finally {
        setLoading(false);
      }
    })();
  }, [token]);

  // Menu tải lại theo ngôn ngữ đã chọn (backend trả tên món từ product_translations)
  useEffect(() => {
    if (!context) return;
    let active = true;
    (async () => {
      try {
        const langParam = context.venue.defaultLocale.toLowerCase().startsWith(lang)
          ? "" : `?lang=${lang}`;
        const menuData = await publicApi<PublicMenu>(
          `/public/venues/${context.venue.slug}/menu${langParam}`,
        );
        if (active) setMenu(menuData);
      } catch (err) {
        if (active && !menu) {
          setError(err instanceof ApiError ? err.message : t(lang, "menuLoadError"));
        }
      }
    })();
    return () => {
      active = false;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [context, lang]);

  // CUS-06: SignalR real-time + polling 30s dự phòng khi mạng yếu
  const seenEventIds = useRef<Set<string>>(new Set());
  useEffect(() => {
    if (!sessionToken) return;
    let active = true;
    const load = async () => {
      try {
        const data = await publicApi<SessionOrders>(
          `/public/table-sessions/${sessionToken}/orders`,
        );
        if (active) setSessionOrders(data);
      } catch {
        /* giữ dữ liệu cũ khi mạng chập chờn */
      }
    };
    load();
    const fallback = setInterval(load, 30000);

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_ORIGIN}/hubs/orders`)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    for (const eventType of [
      "order.created", "order.confirmed", "order.rejected",
      "order.preparing", "order.ready", "order.completed", "order.cancelled",
    ]) {
      connection.on(eventType, (payload: { eventId?: string }) => {
        if (payload.eventId) {
          if (seenEventIds.current.has(payload.eventId)) return;
          seenEventIds.current.add(payload.eventId);
        }
        load();
      });
    }
    connection
      .start()
      .then(() => connection.invoke("JoinTableSession", sessionToken))
      .catch(() => {/* fallback polling vẫn chạy */});
    connection.onreconnected(() => {
      connection.invoke("JoinTableSession", sessionToken).catch(() => {});
      load();
    });

    return () => {
      active = false;
      clearInterval(fallback);
      connection.stop();
    };
  }, [sessionToken]);

  const [serviceNotice, setServiceNotice] = useState("");
  const [search, setSearch] = useState("");
  const [paymentModal, setPaymentModal] = useState(false);

  async function callStaff(type: "call_staff" | "payment") {
    if (!sessionToken) return;
    try {
      await publicApi("/public/service-requests", {
        body: { sessionToken, type },
      });
      setServiceNotice(type === "payment" ? t(lang, "paymentRequested") : t(lang, "staffCalled"));
      setTimeout(() => setServiceNotice(""), 4000);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t(lang, "requestFailed"));
    }
  }

  function requestPayment() {
    callStaff("payment");
    setPaymentModal(true); // CUS-09/ADR-004: hiện QR chuyển khoản nếu quán cấu hình
  }

  // Tìm không phân biệt dấu: "pho" khớp "Phở" (khách nước ngoài gõ không dấu)
  const filteredCategories = useMemo(() => {
    if (!menu) return [];
    const term = normalizeSearch(search.trim());
    if (!term) return menu.categories;
    return menu.categories
      .map((category) => ({
        ...category,
        products: category.products.filter(
          (p) =>
            normalizeSearch(p.name).includes(term) ||
            (p.description && normalizeSearch(p.description).includes(term)),
        ),
      }))
      .filter((category) => category.products.length > 0);
  }, [menu, search]);

  async function submitOrder() {
    if (!sessionToken || cart.length === 0 || submitting) return;
    setSubmitting(true);
    try {
      await publicApi("/public/orders", {
        headers: { "Idempotency-Key": crypto.randomUUID() },
        body: {
          sessionToken,
          clientOrderId: crypto.randomUUID(),
          items: cart.map((i) => ({
            productId: i.productId,
            variantId: i.variantId ?? null,
            quantity: i.quantity,
            note: i.note ?? null,
            optionIds: i.optionIds,
          })),
        },
      });
      setCart([]);
      setCartOpen(false);
      const data = await publicApi<SessionOrders>(
        `/public/table-sessions/${sessionToken}/orders`,
      );
      setSessionOrders(data);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : t(lang, "orderFailed"));
    } finally {
      setSubmitting(false);
    }
  }

  const currency = menu?.venue.currencyCode ?? "VND";
  const itemCount = useMemo(() => cart.reduce((s, i) => s + i.quantity, 0), [cart]);

  function addToCart(item: CartItem) {
    setCart((prev) => {
      const existing = prev.find((x) => x.key === item.key);
      if (existing) {
        return prev.map((x) =>
          x.key === item.key ? { ...x, quantity: x.quantity + item.quantity } : x,
        );
      }
      return [...prev, item];
    });
    setSelected(null);
  }

  function changeQuantity(key: string, delta: number) {
    setCart((prev) =>
      prev
        .map((x) => (x.key === key ? { ...x, quantity: x.quantity + delta } : x))
        .filter((x) => x.quantity > 0),
    );
  }

  if (loading) {
    return <main className="p-8 text-center text-gray-500">{t(lang, "loadingMenu")}</main>;
  }

  if (error) {
    return (
      <main className="flex min-h-screen items-center justify-center bg-orange-50 p-6">
        <div className="rounded-2xl bg-white p-8 text-center shadow">
          <p className="text-4xl">😔</p>
          <p className="mt-2 font-medium">{error}</p>
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-orange-50 pb-24">
      <header className="bg-gradient-to-br from-brand-600 to-orange-500 px-4 pb-4 pt-3 text-white shadow-md">
        <div className="mx-auto max-w-2xl">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0">
              <p className="truncate text-lg font-extrabold">{context?.venue.name}</p>
              <div className="mt-1 flex flex-wrap items-center gap-1.5 text-[11px]">
                <span className="rounded-full bg-white/20 px-2 py-0.5 font-semibold">
                  {context?.area ? `${context.area.name} · ` : ""}
                  {t(lang, "table")} {context?.table.code}
                </span>
                {context?.venue.wifiName && (
                  <span className="rounded-full bg-white/20 px-2 py-0.5">
                    📶 {context.venue.wifiName}
                  </span>
                )}
              </div>
            </div>
            {/* Chuyển ngôn ngữ VI/EN/ZH/JA — select gọn với icon địa cầu */}
            <label className="relative flex shrink-0 items-center">
              <span className="pointer-events-none absolute left-2.5 text-sm">🌐</span>
              <select
                value={lang}
                onChange={(e) => changeLang(e.target.value as Lang)}
                aria-label="Ngôn ngữ / Language"
                className="appearance-none rounded-full bg-white/20 py-1.5 pl-8 pr-7 text-xs font-bold text-white backdrop-blur focus:outline-none focus:ring-2 focus:ring-white/60 [&>option]:text-gray-800"
              >
                {LANGS.map((l) => (
                  <option key={l.code} value={l.code}>
                    {l.nativeName}
                  </option>
                ))}
              </select>
              <svg className="pointer-events-none absolute right-2.5" width="10" height="10"
                viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="3"
                strokeLinecap="round" strokeLinejoin="round">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </label>
          </div>

          {serviceNotice && (
            <p className="mt-2 text-xs font-semibold text-emerald-100">{serviceNotice}</p>
          )}

          {/* Form tìm món: icon + nút xóa, tìm không dấu */}
          <div className="relative mt-3">
            <span className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-sm text-gray-400">
              🔍
            </span>
            <input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t(lang, "searchPlaceholder")}
              className="w-full rounded-full border-0 bg-white py-2.5 pl-10 pr-9 text-sm text-gray-800 shadow focus:outline-none focus:ring-2 focus:ring-white/70 [&::-webkit-search-cancel-button]:hidden"
            />
            {search && (
              <button
                onClick={() => setSearch("")}
                aria-label={t(lang, "clearSearch")}
                className="absolute right-2 top-1/2 flex h-6 w-6 -translate-y-1/2 items-center justify-center rounded-full bg-gray-200 text-xs font-bold text-gray-600"
              >
                ×
              </button>
            )}
          </div>

          <div className="mt-2.5 flex gap-2">
            <button
              onClick={() => callStaff("call_staff")}
              className="flex-1 rounded-full bg-white/15 px-3 py-2 text-xs font-semibold ring-1 ring-white/40 active:bg-white/30"
            >
              {t(lang, "callStaff")}
            </button>
            <button
              onClick={requestPayment}
              className="flex-1 rounded-full bg-white/15 px-3 py-2 text-xs font-semibold ring-1 ring-white/40 active:bg-white/30"
            >
              {t(lang, "requestPayment")}
            </button>
          </div>

          <p className="mt-2 text-[11px] text-white/85">
            {context?.todayHours && (
              <>
                <span className={context.isOpenNow ? "font-bold text-emerald-200" : "font-bold text-red-200"}>
                  {context.isOpenNow ? t(lang, "openNow") : t(lang, "closedNow")}
                </span>
                {" "}· {t(lang, "todayLabel")}: {context.todayHours} ·{" "}
              </>
            )}
            {t(lang, "paymentLabel")}:{" "}
            {context?.venue.paymentMethods
              ?.map((m) => (m === "cash" ? t(lang, "cash") : t(lang, "bankTransfer")))
              .join(" · ") ?? t(lang, "cash")}
          </p>
        </div>
      </header>

      {error && (
        <div className="mx-4 mt-3 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">×</button>
        </div>
      )}

      {sessionOrders && sessionOrders.orders.length > 0 && (
        <TableOrders sessionOrders={sessionOrders} lang={lang} currency={currency} />
      )}

      {/* Chip danh mục cuộn ngang — bấm nhảy tới section (pattern GrabFood) */}
      {!search.trim() && (menu?.categories.length ?? 0) > 1 && (
        <div className="no-scrollbar sticky top-0 z-[5] -mt-px flex gap-2 overflow-x-auto bg-brand-50/95 px-4 py-2 backdrop-blur">
          {menu?.categories.map((category) => (
            <button
              key={category.id}
              onClick={() =>
                document.getElementById(`cat-${category.id}`)?.scrollIntoView({
                  behavior: "smooth", block: "start",
                })
              }
              className="shrink-0 rounded-full border border-brand-200 bg-white px-3.5 py-1.5 text-xs font-semibold text-brand-800 active:bg-brand-100"
            >
              {category.name}
            </button>
          ))}
        </div>
      )}

      <div className="mx-auto max-w-2xl space-y-6 p-4">
        {search.trim() && filteredCategories.length === 0 && (
          <p className="py-8 text-center text-sm text-gray-400">
            {t(lang, "noResults")} “{search.trim()}”. {t(lang, "noResultsHint")}
          </p>
        )}
        {filteredCategories.map((category) => (
          <section key={category.id} id={`cat-${category.id}`} className="scroll-mt-14">
            <h2 className="mb-2 font-semibold">{category.name}</h2>
            <ul className="space-y-2">
              {category.products.map((product) => (
                <li key={product.id}>
                  <button
                    disabled={!product.isAvailable}
                    onClick={() => setSelected(product)}
                    className="flex w-full items-center gap-3 rounded-2xl bg-white p-3 text-left shadow-sm transition-transform active:scale-[0.99] disabled:opacity-50"
                  >
                    {product.imageUrl ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={`${API_ORIGIN}${product.imageUrl}`}
                        alt={product.name}
                        className="h-16 w-16 rounded-lg object-cover"
                      />
                    ) : (
                      <div className="flex h-16 w-16 items-center justify-center rounded-lg bg-orange-100 text-2xl">
                        🍽
                      </div>
                    )}
                    <div className="flex-1">
                      <p className="font-medium">{product.name}</p>
                      {product.description && (
                        <p className="line-clamp-1 text-xs text-gray-500">{product.description}</p>
                      )}
                      <p className="mt-1 text-sm font-semibold text-orange-600">
                        {formatMoney(product.basePriceMinor, currency)}
                      </p>
                    </div>
                    {product.isAvailable ? (
                      <span className="text-2xl text-orange-500">＋</span>
                    ) : (
                      <span className="text-xs text-gray-400">{t(lang, "outOfStock")}</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>

      {/* Footer: thông tin quán + branding TingGo */}
      <footer className="mx-auto max-w-2xl px-4 pb-6 pt-2">
        <div className="rounded-2xl bg-white p-4 text-center shadow-sm">
          <p className="font-bold text-gray-800">{context?.venue.name}</p>
          {context?.todayHours && (
            <p className="mt-1 text-xs text-gray-500">
              {t(lang, "footerHours")}: {context.todayHours}
            </p>
          )}
          <p className="mt-1 text-xs text-gray-500">
            {t(lang, "paymentLabel")}:{" "}
            {context?.venue.paymentMethods
              ?.map((m) => (m === "cash" ? t(lang, "cash") : t(lang, "bankTransfer")))
              .join(" · ") ?? t(lang, "cash")}
          </p>
        </div>
        <p className="mt-4 text-center text-[11px] text-gray-400">
          Powered by <span className="font-bold text-brand-600">TingGo</span> —{" "}
          {t(lang, "footerTagline")}
        </p>
      </footer>

      {itemCount > 0 && (
        <div className="fixed bottom-0 left-0 right-0 z-10 px-4 pb-safe">
          <button
            onClick={() => setCartOpen(true)}
            className="mx-auto flex w-full max-w-2xl items-center justify-between rounded-2xl bg-brand-600 px-5 py-3.5 font-bold text-white shadow-xl shadow-brand-600/30 active:bg-brand-700"
          >
            <span className="flex items-center gap-2">
              <span className="flex h-6 w-6 items-center justify-center rounded-full bg-white/25 text-xs font-extrabold">
                {itemCount}
              </span>
              {t(lang, "viewCart")}
            </span>
            <span>{formatMoney(cartTotal(cart), currency)}</span>
          </button>
        </div>
      )}

      {paymentModal && (
        <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/50 p-6"
          onClick={() => setPaymentModal(false)}>
          <div className="w-full max-w-xs rounded-2xl bg-white p-5 text-center"
            onClick={(e) => e.stopPropagation()}>
            <h3 className="text-lg font-bold">
              {t(lang, "paymentModalTitle")} {context?.table.code}
            </h3>
            {sessionOrders && (
              <p className="mt-1 text-xl font-bold text-orange-600">
                {formatMoney(sessionOrders.totalMinor, currency)}
              </p>
            )}
            {context?.venue.bankQrImageUrl ? (
              <>
                {/* eslint-disable-next-line @next/next/no-img-element */}
                <img src={`${API_ORIGIN}${context.venue.bankQrImageUrl}`} alt="QR chuyển khoản"
                  className="mx-auto my-3 h-52 w-52 rounded-xl border object-contain" />
                <p className="text-xs text-gray-500">{t(lang, "scanQrHint")}</p>
              </>
            ) : (
              <p className="my-4 text-sm text-gray-600">{t(lang, "staffWillCollect")}</p>
            )}
            <button onClick={() => setPaymentModal(false)}
              className="mt-3 w-full rounded-xl bg-orange-600 py-2.5 font-semibold text-white">
              {t(lang, "close")}
            </button>
          </div>
        </div>
      )}

      {selected && (
        <ProductSheet
          product={selected}
          currency={currency}
          lang={lang}
          onClose={() => setSelected(null)}
          onAdd={addToCart}
        />
      )}

      {cartOpen && (
        <div className="fixed inset-0 z-20 flex items-end bg-black/40" onClick={() => setCartOpen(false)}>
          <div
            className="max-h-[80vh] w-full overflow-y-auto rounded-t-3xl bg-white p-4 pb-safe"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-gray-300" />
            <h3 className="mb-3 text-lg font-bold">
              {t(lang, "cartTitle")} {context?.table.code}
            </h3>
            <ul className="space-y-3">
              {cart.map((item) => (
                <li key={item.key} className="flex items-center justify-between gap-2">
                  <div className="flex-1">
                    <p className="text-sm font-medium">
                      {item.productName}
                      {item.variantName ? ` (${item.variantName})` : ""}
                    </p>
                    {item.optionNames.length > 0 && (
                      <p className="text-xs text-gray-500">{item.optionNames.join(", ")}</p>
                    )}
                    {item.note && <p className="text-xs italic text-gray-400">“{item.note}”</p>}
                    <p className="text-xs font-semibold text-orange-600">
                      {formatMoney(item.unitPriceMinor, currency)}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => changeQuantity(item.key, -1)}
                      className="h-7 w-7 rounded-full bg-orange-100 font-bold text-orange-600"
                    >
                      −
                    </button>
                    <span className="w-5 text-center text-sm">{item.quantity}</span>
                    <button
                      onClick={() => changeQuantity(item.key, 1)}
                      className="h-7 w-7 rounded-full bg-orange-100 font-bold text-orange-600"
                    >
                      +
                    </button>
                  </div>
                </li>
              ))}
            </ul>
            <div className="mt-4 flex items-center justify-between border-t pt-3 font-bold">
              <span>{t(lang, "totalLabel")}</span>
              <span className="text-orange-600">{formatMoney(cartTotal(cart), currency)}</span>
            </div>
            <button
              onClick={submitOrder}
              disabled={submitting || !sessionToken}
              className="mt-3 w-full rounded-xl bg-orange-600 py-3 font-semibold text-white disabled:opacity-50"
            >
              {submitting ? t(lang, "submittingOrder") : t(lang, "submitOrder")}
            </button>
          </div>
        </div>
      )}
    </main>
  );
}

const DONE_STATUSES = new Set(["completed", "rejected", "cancelled"]);

/** Trạng thái → màu hiển thị (dot + chip + viền trái card) */
function statusStyle(status: string): { dot: string; chip: string; border: string } {
  switch (status) {
    case "ready":
      return { dot: "bg-success animate-pulse", chip: "bg-success-bg text-success", border: "border-l-success" };
    case "completed":
      return { dot: "bg-gray-400", chip: "bg-gray-100 text-gray-500", border: "border-l-gray-300" };
    case "rejected":
    case "cancelled":
      return { dot: "bg-danger", chip: "bg-danger-bg text-danger", border: "border-l-danger" };
    case "submitted":
      return { dot: "bg-warning animate-pulse", chip: "bg-warning-bg text-warning", border: "border-l-warning" };
    default: // confirmed, preparing
      return { dot: "bg-brand-600", chip: "bg-brand-100 text-brand-700", border: "border-l-brand-500" };
  }
}

interface TableOrdersProps {
  sessionOrders: SessionOrders;
  lang: Lang;
  currency: string;
}

/** Danh sách order của bàn: đơn đang xử lý nổi bật, đơn đã xong thu gọn */
function TableOrders({ sessionOrders, lang, currency }: TableOrdersProps) {
  const [showDone, setShowDone] = useState(false);
  // UUIDv7 tăng theo thời gian → sort desc = đơn mới nhất trước
  const sorted = [...sessionOrders.orders].sort((a, b) => b.id.localeCompare(a.id));
  const active = sorted.filter((o) => !DONE_STATUSES.has(o.status));
  const done = sorted.filter((o) => DONE_STATUSES.has(o.status));

  const renderOrder = (o: SessionOrders["orders"][number]) => {
    const style = statusStyle(o.status);
    return (
      <li key={o.id} className={`rounded-xl border border-l-4 border-gray-100 bg-white p-3 ${style.border}`}>
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm font-bold">{o.orderNumber}</span>
          <span className={`flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ${style.chip}`}>
            <span className={`h-1.5 w-1.5 rounded-full ${style.dot}`} />
            {statusLabel(lang, o.status)}
          </span>
        </div>
        <ul className="mt-1.5 space-y-0.5 text-xs text-gray-600">
          {o.items.map((i, idx) => (
            <li key={idx}>{i.quantity}× {i.productName}</li>
          ))}
        </ul>
        <p className="mt-1.5 text-right text-xs font-semibold text-gray-700">
          {formatMoney(o.totalMinor, currency)}
        </p>
      </li>
    );
  };

  return (
    <section className="mx-auto mt-3 max-w-2xl rounded-2xl bg-white p-3 shadow-sm max-sm:mx-4">
      <h2 className="mb-2 text-sm font-semibold">{t(lang, "tableOrders")}</h2>
      {active.length > 0 && <ul className="space-y-2">{active.map(renderOrder)}</ul>}

      {done.length > 0 && (
        <>
          <button
            onClick={() => setShowDone((v) => !v)}
            className="mt-2 flex w-full items-center justify-between rounded-lg bg-gray-50 px-3 py-2 text-xs font-medium text-gray-500"
          >
            <span>{t(lang, "doneOrders")} ({done.length})</span>
            <span className="text-brand-600">{showDone ? t(lang, "hideLabel") : t(lang, "showLabel")}</span>
          </button>
          {showDone && <ul className="mt-2 space-y-2">{done.map(renderOrder)}</ul>}
        </>
      )}

      <div className="mt-3 flex items-center justify-between rounded-xl bg-brand-50 px-3 py-2.5">
        <span className="text-sm font-semibold">{t(lang, "tableTotal")}</span>
        <span className="text-base font-extrabold text-brand-600">
          {formatMoney(sessionOrders.totalMinor, currency)}
        </span>
      </div>
    </section>
  );
}

interface ProductSheetProps {
  product: PublicProduct;
  currency: string;
  lang: Lang;
  onClose: () => void;
  onAdd: (item: CartItem) => void;
}

function ProductSheet({ product, currency, lang, onClose, onAdd }: ProductSheetProps) {
  const defaultVariant = product.variants.find((v) => v.isDefault) ?? product.variants[0];
  const [variantId, setVariantId] = useState<string | undefined>(defaultVariant?.id);
  const [optionIds, setOptionIds] = useState<string[]>([]);
  const [note, setNote] = useState("");
  const [quantity, setQuantity] = useState(1);

  const variant = product.variants.find((v) => v.id === variantId);
  const unitPrice =
    product.basePriceMinor +
    (variant?.priceDeltaMinor ?? 0) +
    product.modifierGroups
      .flatMap((g) => g.options)
      .filter((o) => optionIds.includes(o.id))
      .reduce((s, o) => s + o.priceDeltaMinor, 0);

  function toggleOption(groupMax: number, groupOptionIds: string[], optionId: string) {
    setOptionIds((prev) => {
      if (prev.includes(optionId)) return prev.filter((x) => x !== optionId);
      const selectedInGroup = prev.filter((x) => groupOptionIds.includes(x));
      if (selectedInGroup.length >= groupMax) {
        // Thay lựa chọn cũ nhất trong nhóm khi vượt max
        return [...prev.filter((x) => x !== selectedInGroup[0]), optionId];
      }
      return [...prev, optionId];
    });
  }

  function confirm() {
    const optionNames = product.modifierGroups
      .flatMap((g) => g.options)
      .filter((o) => optionIds.includes(o.id))
      .map((o) => o.name);
    onAdd({
      key: `${product.id}|${variantId ?? ""}|${[...optionIds].sort().join(",")}|${note}`,
      productId: product.id,
      productName: product.name,
      variantId,
      variantName: variant?.name,
      optionIds,
      optionNames,
      note: note || undefined,
      quantity,
      unitPriceMinor: unitPrice,
    });
  }

  return (
    <div className="fixed inset-0 z-20 flex items-end bg-black/40" onClick={onClose}>
      <div
        className="max-h-[85vh] w-full overflow-y-auto rounded-t-3xl bg-white p-4 pb-safe"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-gray-300" />
        <h3 className="text-lg font-bold">{product.name}</h3>
        {product.description && <p className="text-sm text-gray-500">{product.description}</p>}

        {product.variants.length > 0 && (
          <div className="mt-3">
            <p className="mb-1 text-sm font-semibold">{t(lang, "size")}</p>
            <div className="flex flex-wrap gap-2">
              {product.variants.map((v) => (
                <button
                  key={v.id}
                  onClick={() => setVariantId(v.id)}
                  className={`rounded-full border px-3 py-1 text-sm ${
                    variantId === v.id
                      ? "border-orange-600 bg-orange-600 text-white"
                      : "border-gray-300"
                  }`}
                >
                  {v.name}
                  {v.priceDeltaMinor !== 0 &&
                    ` ${v.priceDeltaMinor > 0 ? "+" : ""}${formatMoney(v.priceDeltaMinor, currency)}`}
                </button>
              ))}
            </div>
          </div>
        )}

        {product.modifierGroups.map((group) => {
          const groupOptionIds = group.options.map((o) => o.id);
          return (
            <div key={group.id} className="mt-3">
              <p className="mb-1 text-sm font-semibold">
                {group.name}
                <span className="ml-1 text-xs font-normal text-gray-400">
                  ({t(lang, "maxSelect")} {group.maxSelect})
                </span>
              </p>
              <div className="flex flex-wrap gap-2">
                {group.options.map((option) => (
                  <button
                    key={option.id}
                    onClick={() => toggleOption(group.maxSelect, groupOptionIds, option.id)}
                    className={`rounded-full border px-3 py-1 text-sm ${
                      optionIds.includes(option.id)
                        ? "border-orange-600 bg-orange-600 text-white"
                        : "border-gray-300"
                    }`}
                  >
                    {option.name}
                    {option.priceDeltaMinor > 0 &&
                      ` +${formatMoney(option.priceDeltaMinor, currency)}`}
                  </button>
                ))}
              </div>
            </div>
          );
        })}

        <input
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder={t(lang, "notePlaceholder")}
          maxLength={200}
          className="mt-3 w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-orange-500 focus:outline-none"
        />

        <div className="mt-4 flex items-center gap-3">
          <div className="flex items-center gap-2">
            <button
              onClick={() => setQuantity(Math.max(1, quantity - 1))}
              className="h-9 w-9 rounded-full bg-orange-100 text-lg font-bold text-orange-600"
            >
              −
            </button>
            <span className="w-6 text-center font-semibold">{quantity}</span>
            <button
              onClick={() => setQuantity(quantity + 1)}
              className="h-9 w-9 rounded-full bg-orange-100 text-lg font-bold text-orange-600"
            >
              +
            </button>
          </div>
          <button
            onClick={confirm}
            className="flex-1 rounded-xl bg-orange-600 py-3 font-semibold text-white"
          >
            {t(lang, "addLabel")} · {formatMoney(unitPrice * quantity, currency)}
          </button>
        </div>
      </div>
    </div>
  );
}
