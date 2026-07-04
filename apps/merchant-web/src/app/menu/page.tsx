"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, clearTokens, getTokens } from "@/lib/api";
import type { Category, Membership, Menu, Product, Venue } from "@/lib/types";
import { formatMoney } from "@/lib/types";
import ProductEditModal from "@/components/ProductEditModal";

interface ImportResult {
  menuCreated: boolean;
  menuPublished: boolean;
  categoriesCreated: number;
  productsCreated: number;
  productsSkipped: number;
  areasCreated: number;
  tablesCreated: number;
  tablesSkipped: number;
  errors: string[];
}

interface MenuDetail extends Menu {
  categories: Category[];
}

export default function MenuPage() {
  const router = useRouter();
  const [venues, setVenues] = useState<Venue[]>([]);
  const [venue, setVenue] = useState<Venue | null>(null);
  const [menus, setMenus] = useState<Menu[]>([]);
  const [menu, setMenu] = useState<MenuDetail | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [editingProductId, setEditingProductId] = useState<string | null>(null);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [importing, setImporting] = useState(false);

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

  const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";

  async function downloadTemplate() {
    if (!venue) return;
    const res = await fetch(`${apiBase}/venues/${venue.id}/import/template`, {
      headers: { Authorization: `Bearer ${getTokens()?.accessToken}` },
    });
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = "tinggo-mau-nhap-lieu.xlsx";
    a.click();
    URL.revokeObjectURL(url);
  }

  async function importFile(file: File) {
    if (!venue) return;
    setImporting(true);
    setError("");
    try {
      const formData = new FormData();
      formData.append("file", file);
      const result = await api<ImportResult>(`/venues/${venue.id}/import`, { formData });
      setImportResult(result);
      await loadMenus(venue);
      setProducts(await api<Product[]>(`/venues/${venue.id}/products`));
    } catch (err) {
      showError(err);
    } finally {
      setImporting(false);
    }
  }

  // Nạp venue từ memberships
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
        const orgIds = [...new Set(memberships.map((m) => m.organizationId))];
        const all: Venue[] = [];
        for (const orgId of orgIds) {
          all.push(...(await api<Venue[]>(`/organizations/${orgId}/venues`)));
        }
        setVenues(all);
        setVenue(all[0] ?? null);
      } catch (err) {
        showError(err);
      } finally {
        setLoading(false);
      }
    })();
  }, [router]);

  // Nạp menus khi đổi venue
  const loadMenus = useCallback(async (v: Venue) => {
    const items = await api<Menu[]>(`/venues/${v.id}/menus`);
    setMenus(items);
    if (items.length > 0) {
      setMenu(await api<MenuDetail>(`/menus/${items[0].id}`));
    } else {
      setMenu(null);
    }
  }, []);

  useEffect(() => {
    if (!venue) return;
    (async () => {
      try {
        await loadMenus(venue);
        setProducts(await api<Product[]>(`/venues/${venue.id}/products`));
      } catch (err) {
        showError(err);
      }
    })();
  }, [venue, loadMenus]);

  async function refreshMenu() {
    if (menu) setMenu(await api<MenuDetail>(`/menus/${menu.id}`));
  }

  async function refreshProducts() {
    if (venue) setProducts(await api<Product[]>(`/venues/${venue.id}/products`));
  }

  async function createMenu() {
    if (!venue) return;
    try {
      await api(`/venues/${venue.id}/menus`, { body: { name: "Menu chính" } });
      await loadMenus(venue);
    } catch (err) {
      showError(err);
    }
  }

  async function addCategory(name: string) {
    if (!menu) return;
    try {
      await api(`/menus/${menu.id}/categories`, { body: { name } });
      await refreshMenu();
    } catch (err) {
      showError(err);
    }
  }

  async function togglePublish() {
    if (!menu) return;
    try {
      await api(`/menus/${menu.id}/${menu.status === "published" ? "unpublish" : "publish"}`, {
        method: "POST",
      });
      await refreshMenu();
    } catch (err) {
      showError(err);
    }
  }

  async function toggleAvailability(product: Product) {
    try {
      await api(`/products/${product.id}/availability`, {
        method: "PATCH",
        body: { isAvailable: !product.isAvailable },
      });
      await refreshProducts();
    } catch (err) {
      showError(err);
    }
  }

  function logout() {
    clearTokens();
    router.push("/login");
  }

  if (loading) {
    return <main className="p-8 text-gray-500">Đang tải...</main>;
  }

  return (
    <main className="min-h-screen bg-orange-50">
      <header className="flex items-center justify-between border-b bg-white px-6 py-3">
        <div className="flex items-center gap-4">
          <span className="text-xl font-bold text-orange-600">TingGo</span>
          <nav className="flex gap-3 text-sm">
            <span className="font-semibold text-orange-600">Menu</span>
            <a href="/tables" className="text-gray-500 hover:text-orange-600">
              Bàn & QR
            </a>
            <a href="/orders" className="text-gray-500 hover:text-orange-600">
              Order
            </a>
          </nav>
          {venues.length > 0 && (
            <select
              value={venue?.id ?? ""}
              onChange={(e) => setVenue(venues.find((v) => v.id === e.target.value) ?? null)}
              className="rounded-lg border border-gray-300 px-2 py-1 text-sm"
            >
              {venues.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </select>
          )}
        </div>
        <button onClick={logout} className="text-sm text-gray-500 hover:underline">
          Đăng xuất
        </button>
      </header>

      {error && (
        <div className="mx-6 mt-4 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">
            ×
          </button>
        </div>
      )}

      <div className="mx-6 mt-4 flex flex-wrap items-center gap-2 rounded-2xl bg-white p-3 shadow">
        <span className="text-sm font-semibold">Nhập dữ liệu nhanh:</span>
        <button onClick={downloadTemplate} className="rounded-lg border border-orange-400 px-3 py-1.5 text-sm text-orange-600 hover:bg-orange-50">
          ⬇ Tải file mẫu Excel
        </button>
        <label className="cursor-pointer rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-semibold text-white hover:bg-orange-700">
          {importing ? "Đang nhập..." : "⬆ Nhập từ Excel"}
          <input
            type="file"
            accept=".xlsx"
            className="hidden"
            disabled={importing}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) importFile(f);
              e.target.value = "";
            }}
          />
        </label>
        <span className="text-xs text-gray-400">Món + danh mục + size + topping + khu vực + bàn trong 1 file</span>
      </div>

      {importResult && (
        <div className="mx-6 mt-3 rounded-2xl bg-green-50 p-4 text-sm shadow ring-1 ring-green-200">
          <p className="font-semibold text-green-800">Kết quả nhập:</p>
          <p>
            {importResult.categoriesCreated} danh mục, {importResult.productsCreated} món
            {importResult.productsSkipped > 0 && ` (bỏ qua ${importResult.productsSkipped} món trùng)`},{" "}
            {importResult.areasCreated} khu vực, {importResult.tablesCreated} bàn
            {importResult.tablesSkipped > 0 && ` (bỏ qua ${importResult.tablesSkipped} bàn trùng mã)`}.
            {importResult.menuPublished && " Menu đã được công bố."}
          </p>
          {importResult.tablesCreated > 0 && (
            <p className="mt-1">
              → Sang trang <a href="/tables" className="font-semibold text-orange-600 underline">Bàn & QR</a> để in QR cho bàn mới.
            </p>
          )}
          {importResult.errors.length > 0 && (
            <ul className="mt-1 list-inside list-disc text-red-600">
              {importResult.errors.map((e, i) => <li key={i}>{e}</li>)}
            </ul>
          )}
          <button onClick={() => setImportResult(null)} className="mt-2 text-xs text-gray-500 underline">Đóng</button>
        </div>
      )}

      <div className="grid gap-6 p-6 lg:grid-cols-[300px_1fr]">
        {/* Menu + danh mục */}
        <section className="rounded-2xl bg-white p-4 shadow">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="font-semibold">Menu</h2>
            {menu && (
              <button
                onClick={togglePublish}
                className={`rounded-full px-3 py-1 text-xs font-semibold ${
                  menu.status === "published"
                    ? "bg-green-100 text-green-700"
                    : "bg-gray-200 text-gray-600"
                }`}
              >
                {menu.status === "published" ? "Đang công bố ✓" : "Bản nháp — nhấn để công bố"}
              </button>
            )}
          </div>

          {menus.length === 0 ? (
            <button
              onClick={createMenu}
              className="w-full rounded-lg border-2 border-dashed border-orange-300 py-3 text-sm text-orange-600 hover:bg-orange-50"
            >
              + Tạo menu đầu tiên
            </button>
          ) : (
            menu && (
              <>
                <p className="mb-2 text-sm font-medium">{menu.name}</p>
                <ul className="space-y-1">
                  {menu.categories.map((c) => (
                    <li
                      key={c.id}
                      className="flex items-center justify-between rounded-lg bg-orange-50 px-3 py-2 text-sm"
                    >
                      <span className={c.isVisible ? "" : "text-gray-400 line-through"}>
                        {c.name}
                      </span>
                      <span className="text-xs text-gray-400">
                        {products.filter((p) => p.categoryId === c.id).length} món
                      </span>
                    </li>
                  ))}
                </ul>
                <CategoryForm onAdd={addCategory} />
              </>
            )
          )}
        </section>

        {/* Món */}
        <section className="rounded-2xl bg-white p-4 shadow">
          <h2 className="mb-3 font-semibold">Món ({products.length})</h2>
          {menu && menu.categories.length > 0 && venue && (
            <ProductForm
              categories={menu.categories}
              venueId={venue.id}
              onCreated={refreshProducts}
              onError={showError}
            />
          )}
          <ul className="mt-4 grid gap-2 md:grid-cols-2">
            {products.map((p) => (
              <li key={p.id} className="flex items-center gap-3 rounded-xl border p-3">
                {p.imageUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={`${(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1").replace("/api/v1", "")}${p.imageUrl}`}
                    alt={p.name}
                    className="h-12 w-12 rounded-lg object-cover"
                  />
                ) : (
                  <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-orange-100 text-lg">
                    🍽
                  </div>
                )}
                <div className="flex-1">
                  <p className="text-sm font-medium">{p.name}</p>
                  <p className="text-xs text-gray-500">
                    {formatMoney(p.basePriceMinor, p.currencyCode)}
                  </p>
                </div>
                <button
                  onClick={() => toggleAvailability(p)}
                  className={`rounded-full px-3 py-1 text-xs font-semibold ${
                    p.isAvailable ? "bg-green-100 text-green-700" : "bg-gray-200 text-gray-500"
                  }`}
                >
                  {p.isAvailable ? "Còn bán" : "Hết hàng"}
                </button>
                <button
                  onClick={() => setEditingProductId(p.id)}
                  className="rounded-full border border-gray-300 px-3 py-1 text-xs font-semibold text-gray-600 hover:bg-gray-50"
                >
                  Sửa
                </button>
              </li>
            ))}
          </ul>
          {products.length === 0 && (
            <p className="py-8 text-center text-sm text-gray-400">
              Chưa có món nào. Tạo danh mục rồi thêm món đầu tiên nhé.
            </p>
          )}
        </section>
      </div>

      {editingProductId && venue && menu && (
        <ProductEditModal
          productId={editingProductId}
          venueId={venue.id}
          categories={menu.categories}
          onClose={() => setEditingProductId(null)}
          onSaved={refreshProducts}
        />
      )}
    </main>
  );
}

function CategoryForm({ onAdd }: { onAdd: (name: string) => Promise<void> }) {
  const [name, setName] = useState("");
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        if (!name.trim()) return;
        await onAdd(name.trim());
        setName("");
      }}
      className="mt-3 flex gap-2"
    >
      <input
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Thêm danh mục..."
        maxLength={200}
        className="flex-1 rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-orange-500 focus:outline-none"
      />
      <button className="rounded-lg bg-orange-600 px-3 text-sm font-semibold text-white hover:bg-orange-700">
        +
      </button>
    </form>
  );
}

function ProductForm({
  categories,
  venueId,
  onCreated,
  onError,
}: {
  categories: Category[];
  venueId: string;
  onCreated: () => Promise<void>;
  onError: (err: unknown) => void;
}) {
  const [name, setName] = useState("");
  const [price, setPrice] = useState("");
  const [categoryId, setCategoryId] = useState(categories[0]?.id ?? "");
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!categoryId && categories[0]) setCategoryId(categories[0].id);
  }, [categories, categoryId]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    try {
      let imageUrl: string | undefined;
      if (file) {
        const formData = new FormData();
        formData.append("file", file);
        const uploaded = await api<{ url: string }>("/files/images", { formData });
        imageUrl = uploaded.url;
      }
      await api(`/venues/${venueId}/products`, {
        body: {
          categoryId,
          name: name.trim(),
          basePriceMinor: Math.round(Number(price)),
          imageUrl,
        },
      });
      setName("");
      setPrice("");
      setFile(null);
      await onCreated();
    } catch (err) {
      onError(err);
    } finally {
      setLoading(false);
    }
  }

  return (
    <form onSubmit={submit} className="grid gap-2 rounded-xl bg-orange-50 p-3 md:grid-cols-5">
      <input
        required
        value={name}
        onChange={(e) => setName(e.target.value)}
        placeholder="Tên món"
        maxLength={200}
        className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-orange-500 focus:outline-none md:col-span-2"
      />
      <input
        required
        type="number"
        min={0}
        step={1}
        value={price}
        onChange={(e) => setPrice(e.target.value)}
        placeholder="Giá (đ)"
        className="rounded-lg border border-gray-300 px-3 py-1.5 text-sm focus:border-orange-500 focus:outline-none"
      />
      <select
        value={categoryId}
        onChange={(e) => setCategoryId(e.target.value)}
        className="rounded-lg border border-gray-300 px-2 py-1.5 text-sm"
      >
        {categories.map((c) => (
          <option key={c.id} value={c.id}>
            {c.name}
          </option>
        ))}
      </select>
      <div className="flex items-center gap-2">
        <label className="cursor-pointer rounded-lg border border-gray-300 px-2 py-1.5 text-xs text-gray-600 hover:bg-white">
          {file ? "✓ Ảnh" : "＋Ảnh"}
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="hidden"
          />
        </label>
        <button
          disabled={loading}
          className="flex-1 rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-semibold text-white hover:bg-orange-700 disabled:opacity-50"
        >
          {loading ? "..." : "Thêm"}
        </button>
      </div>
    </form>
  );
}
