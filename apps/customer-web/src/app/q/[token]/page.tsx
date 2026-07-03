"use client";

import { use, useEffect, useMemo, useState } from "react";
import { publicApi, ApiError, formatMoney, API_ORIGIN } from "@/lib/api";
import type {
  CartItem,
  PublicMenu,
  PublicProduct,
  QrContext,
} from "@/lib/types";
import { cartTotal } from "@/lib/types";

export default function QrPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const [context, setContext] = useState<QrContext | null>(null);
  const [menu, setMenu] = useState<PublicMenu | null>(null);
  const [cart, setCart] = useState<CartItem[]>([]);
  const [selected, setSelected] = useState<PublicProduct | null>(null);
  const [cartOpen, setCartOpen] = useState(false);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const qrContext = await publicApi<QrContext>(`/public/q/${token}`);
        setContext(qrContext);
        setMenu(await publicApi<PublicMenu>(`/public/venues/${qrContext.venue.slug}/menu`));
      } catch (err) {
        setError(err instanceof ApiError ? err.message : "Không tải được menu.");
      } finally {
        setLoading(false);
      }
    })();
  }, [token]);

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
        <p className="text-lg font-bold text-orange-600">{context?.venue.name}</p>
        <p className="text-xs text-gray-500">
          {context?.area ? `${context.area.name} · ` : ""}Bàn {context?.table.code}
          {context?.venue.wifiName ? ` · Wi-Fi: ${context.venue.wifiName}` : ""}
        </p>
      </header>

      <div className="space-y-6 p-4">
        {menu?.categories.map((category) => (
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
              disabled
              className="mt-3 w-full rounded-xl bg-gray-300 py-3 font-semibold text-gray-500"
              title="Gửi order sẽ mở ở Sprint 5"
            >
              Gửi order (sắp ra mắt)
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
