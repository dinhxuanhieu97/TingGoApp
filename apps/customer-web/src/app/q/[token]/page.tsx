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

const STATUS_LABEL: Record<string, string> = {
  submitted: "Đã gửi — chờ quán nhận",
  confirmed: "Quán đã nhận ✓",
  preparing: "Đang chuẩn bị 👨‍🍳",
  ready: "Món đã sẵn sàng 🔔",
  completed: "Hoàn thành ✓",
  rejected: "Quán từ chối",
  cancelled: "Đã hủy",
};

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

  useEffect(() => {
    (async () => {
      try {
        const qrContext = await publicApi<QrContext>(`/public/q/${token}`);
        setContext(qrContext);
        const [menuData, session] = await Promise.all([
          publicApi<PublicMenu>(`/public/venues/${qrContext.venue.slug}/menu`),
          publicApi<{ sessionToken: string }>("/public/table-sessions", {
            body: { qrToken: token },
          }),
        ]);
        setMenu(menuData);
        setSessionToken(session.sessionToken);
      } catch (err) {
        setError(err instanceof ApiError ? err.message : "Không tải được menu.");
      } finally {
        setLoading(false);
      }
    })();
  }, [token]);

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
      setServiceNotice(type === "payment" ? "Đã gửi yêu cầu thanh toán 💰" : "Đã gọi nhân viên 🔔");
      setTimeout(() => setServiceNotice(""), 4000);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Không gửi được yêu cầu.");
    }
  }

  function requestPayment() {
    callStaff("payment");
    setPaymentModal(true); // CUS-09/ADR-004: hiện QR chuyển khoản nếu quán cấu hình
  }

  const filteredCategories = useMemo(() => {
    if (!menu) return [];
    const term = search.trim().toLowerCase();
    if (!term) return menu.categories;
    return menu.categories
      .map((category) => ({
        ...category,
        products: category.products.filter((p) => p.name.toLowerCase().includes(term)),
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
      setError(err instanceof ApiError ? err.message : "Gửi order thất bại. Vui lòng thử lại.");
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
    return <main className="p-8 text-center text-gray-500">Đang tải menu...</main>;
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
      <header className="sticky top-0 z-10 bg-white px-4 py-3 shadow-sm">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-lg font-bold text-orange-600">{context?.venue.name}</p>
            <p className="text-xs text-gray-500">
              {context?.area ? `${context.area.name} · ` : ""}Bàn {context?.table.code}
              {context?.venue.wifiName ? ` · Wi-Fi: ${context.venue.wifiName}` : ""}
            </p>
          </div>
          <div className="flex gap-2">
            <button
              onClick={() => callStaff("call_staff")}
              className="rounded-full border border-orange-300 px-3 py-1.5 text-xs font-semibold text-orange-600 hover:bg-orange-50"
            >
              🔔 Gọi nhân viên
            </button>
            <button
              onClick={requestPayment}
              className="rounded-full border border-orange-300 px-3 py-1.5 text-xs font-semibold text-orange-600 hover:bg-orange-50"
            >
              💰 Thanh toán
            </button>
          </div>
        </div>
        {serviceNotice && (
          <p className="mt-1 text-xs font-medium text-green-600">{serviceNotice}</p>
        )}
        <input
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="🔍 Tìm món..."
          className="mt-2 w-full rounded-full border border-gray-200 bg-gray-50 px-4 py-2 text-sm focus:border-orange-400 focus:outline-none"
        />
        <p className="mt-1.5 text-[11px] text-gray-400">
          {context?.todayHours && (
            <>
              <span className={context.isOpenNow ? "font-semibold text-green-600" : "font-semibold text-red-500"}>
                {context.isOpenNow ? "● Đang mở cửa" : "● Ngoài giờ"}
              </span>
              {" "}· Hôm nay: {context.todayHours} ·{" "}
            </>
          )}
          Thanh toán:{" "}
          {context?.venue.paymentMethods
            ?.map((m) => (m === "cash" ? "Tiền mặt" : "Chuyển khoản QR"))
            .join(" · ") ?? "Tiền mặt"}
        </p>
      </header>

      {error && (
        <div className="mx-4 mt-3 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">×</button>
        </div>
      )}

      {sessionOrders && sessionOrders.orders.length > 0 && (
        <section className="mx-4 mt-3 rounded-xl bg-white p-3 shadow-sm">
          <h2 className="mb-2 text-sm font-semibold">Order của bàn</h2>
          <ul className="space-y-2">
            {sessionOrders.orders.map((o) => (
              <li key={o.id} className="flex items-center justify-between text-sm">
                <div>
                  <span className="font-medium">{o.orderNumber}</span>
                  <span className="ml-2 text-xs text-gray-500">
                    {o.items.map((i) => `${i.productName} ×${i.quantity}`).join(", ")}
                  </span>
                </div>
                <span
                  className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                    o.status === "rejected" || o.status === "cancelled"
                      ? "bg-red-100 text-red-600"
                      : o.status === "completed"
                        ? "bg-green-100 text-green-700"
                        : "bg-orange-100 text-orange-700"
                  }`}
                >
                  {STATUS_LABEL[o.status] ?? o.status}
                </span>
              </li>
            ))}
          </ul>
          <p className="mt-2 border-t pt-2 text-right text-sm font-semibold">
            Tổng bàn: {formatMoney(sessionOrders.totalMinor, currency)}
          </p>
        </section>
      )}

      <div className="space-y-6 p-4">
        {search.trim() && filteredCategories.length === 0 && (
          <p className="py-8 text-center text-sm text-gray-400">
            Không tìm thấy món nào cho “{search.trim()}”.
          </p>
        )}
        {filteredCategories.map((category) => (
          <section key={category.id}>
            <h2 className="mb-2 font-semibold">{category.name}</h2>
            <ul className="space-y-2">
              {category.products.map((product) => (
                <li key={product.id}>
                  <button
                    disabled={!product.isAvailable}
                    onClick={() => setSelected(product)}
                    className="flex w-full items-center gap-3 rounded-xl bg-white p-3 text-left shadow-sm disabled:opacity-50"
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
                      <span className="text-xs text-gray-400">Hết hàng</span>
                    )}
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))}
      </div>

      {itemCount > 0 && (
        <button
          onClick={() => setCartOpen(true)}
          className="fixed bottom-4 left-4 right-4 flex items-center justify-between rounded-2xl bg-orange-600 px-5 py-3 font-semibold text-white shadow-lg"
        >
          <span>🛒 {itemCount} món</span>
          <span>{formatMoney(cartTotal(cart), currency)}</span>
        </button>
      )}

      {paymentModal && (
        <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/50 p-6"
          onClick={() => setPaymentModal(false)}>
          <div className="w-full max-w-xs rounded-2xl bg-white p-5 text-center"
            onClick={(e) => e.stopPropagation()}>
            <h3 className="text-lg font-bold">Thanh toán — Bàn {context?.table.code}</h3>
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
                <p className="text-xs text-gray-500">
                  Quét QR để chuyển khoản, hoặc thanh toán tiền mặt.
                  Nhân viên sẽ đến xác nhận với bạn.
                </p>
              </>
            ) : (
              <p className="my-4 text-sm text-gray-600">
                Nhân viên sẽ đến thu tiền tại bàn. Cảm ơn bạn! 🙏
              </p>
            )}
            <button onClick={() => setPaymentModal(false)}
              className="mt-3 w-full rounded-xl bg-orange-600 py-2.5 font-semibold text-white">
              Đóng
            </button>
          </div>
        </div>
      )}

      {selected && (
        <ProductSheet
          product={selected}
          currency={currency}
          onClose={() => setSelected(null)}
          onAdd={addToCart}
        />
      )}

      {cartOpen && (
        <div className="fixed inset-0 z-20 flex items-end bg-black/40" onClick={() => setCartOpen(false)}>
          <div
            className="max-h-[80vh] w-full overflow-y-auto rounded-t-2xl bg-white p-4"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="mb-3 text-lg font-bold">Giỏ hàng — Bàn {context?.table.code}</h3>
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
              <span>Tổng cộng</span>
              <span className="text-orange-600">{formatMoney(cartTotal(cart), currency)}</span>
            </div>
            <button
              onClick={submitOrder}
              disabled={submitting || !sessionToken}
              className="mt-3 w-full rounded-xl bg-orange-600 py-3 font-semibold text-white disabled:opacity-50"
            >
              {submitting ? "Đang gửi..." : "Gửi order"}
            </button>
          </div>
        </div>
      )}
    </main>
  );
}

function ProductSheet({
  product,
  currency,
  onClose,
  onAdd,
}: {
  product: PublicProduct;
  currency: string;
  onClose: () => void;
  onAdd: (item: CartItem) => void;
}) {
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
        className="max-h-[85vh] w-full overflow-y-auto rounded-t-2xl bg-white p-4"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="text-lg font-bold">{product.name}</h3>
        {product.description && <p className="text-sm text-gray-500">{product.description}</p>}

        {product.variants.length > 0 && (
          <div className="mt-3">
            <p className="mb-1 text-sm font-semibold">Size</p>
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
                  (chọn tối đa {group.maxSelect})
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
          placeholder="Ghi chú (VD: ít đá)"
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
            Thêm · {formatMoney(unitPrice * quantity, currency)}
          </button>
        </div>
      </div>
    </div>
  );
}
