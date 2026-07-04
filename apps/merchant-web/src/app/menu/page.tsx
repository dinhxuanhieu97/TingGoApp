"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, clearTokens, getTokens } from "@/lib/api";
import type { Category, Membership, Menu, Product, Venue } from "@/lib/types";
import { formatMoney } from "@/lib/types";
import ProductEditModal from "@/components/ProductEditModal";
import MerchantNav from "@/components/MerchantNav";

interface ImportSummary {
  importId: string;
  status: string;
  totalRows: number;
  validRows: number;
  warningRows: number;
  errorRows: number;
  sections: { section: string; total: number; errors: number; warnings: number }[];
  canCommit: boolean;
}

interface ImportIssueView {
  severity: string;
  code: string;
  sheetName?: string;
  rowNumber?: number;
  fieldName?: string;
  message: string;
}

interface CommitOutcome {
  areasCreated: number;
  tablesCreated: number;
  categoriesCreated: number;
  productsCreated: number;
  variantsCreated: number;
  groupsCreated: number;
  optionsCreated: number;
  menuCreated: boolean;
}

const SECTION_LABEL: Record<string, string> = {
  VENUE: "Thông tin quán",
  AREAS: "Khu vực",
  TABLES: "Bàn",
  CATEGORIES: "Danh mục",
  PRODUCTS: "Món",
  VARIANTS: "Size",
  MODIFIER_GROUPS: "Nhóm tùy chọn",
  MODIFIER_OPTIONS: "Lựa chọn",
  PRODUCT_MODIFIERS: "Liên kết món-nhóm",
};

interface MenuDetail extends Menu {
  categories: Category[];
}

/** Chuẩn hóa tìm kiếm: thường hóa + bỏ dấu tiếng Việt */
function normalizeSearch(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/đ/g, "d");
}

export default function MenuPage() {
  const router = useRouter();
  const [venues, setVenues] = useState<Venue[]>([]);
  const [venue, setVenue] = useState<Venue | null>(null);
  const [menus, setMenus] = useState<Menu[]>([]);
  const [menu, setMenu] = useState<MenuDetail | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [productSearch, setProductSearch] = useState("");

  // Tìm không phân biệt dấu ("pho" khớp "Phở")
  const visibleProducts = useMemo(() => {
    const term = normalizeSearch(productSearch.trim());
    if (!term) return products;
    return products.filter((p) => normalizeSearch(p.name).includes(term));
  }, [products, productSearch]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [editingProductId, setEditingProductId] = useState<string | null>(null);
  const [importSummary, setImportSummary] = useState<ImportSummary | null>(null);
  const [importIssues, setImportIssues] = useState<ImportIssueView[]>([]);
  const [commitOutcome, setCommitOutcome] = useState<CommitOutcome | null>(null);
  const [importing, setImporting] = useState(false);
  const [committing, setCommitting] = useState(false);

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

  const apiBase = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1";

  async function downloadAuthorizedFile(path: string, filename: string) {
    const res = await fetch(`${apiBase}${path}`, {
      headers: { Authorization: `Bearer ${getTokens()?.accessToken}` },
    });
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  async function importFile(file: File) {
    if (!venue) return;
    setImporting(true);
    setError("");
    setCommitOutcome(null);
    try {
      const formData = new FormData();
      formData.append("file", file);
      const summary = await api<ImportSummary>(`/venues/${venue.id}/imports`, { formData });
      setImportSummary(summary);
      setImportIssues(await api<ImportIssueView[]>(`/imports/${summary.importId}/issues`));
    } catch (err) {
      showError(err);
    } finally {
      setImporting(false);
    }
  }

  async function commitImport() {
    if (!venue || !importSummary) return;
    setCommitting(true);
    try {
      const result = await api<{ status: string; outcome: CommitOutcome }>(
        `/imports/${importSummary.importId}/commit`,
        { method: "POST" },
      );
      setCommitOutcome(result.outcome);
      setImportSummary(null);
      setImportIssues([]);
      await loadMenus(venue);
      setProducts(await api<Product[]>(`/venues/${venue.id}/products`));
    } catch (err) {
      showError(err);
    } finally {
      setCommitting(false);
    }
  }

  async function cancelImport() {
    if (!importSummary) return;
    try {
      await api(`/imports/${importSummary.importId}/cancel`, { method: "POST" });
    } catch {
      /* job có thể đã hủy */
    }
    setImportSummary(null);
    setImportIssues([]);
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

  async function moveProduct(product: Product, direction: -1 | 1) {
    const sameCategory = products.filter((p) => p.categoryId === product.categoryId);
    const index = sameCategory.findIndex((p) => p.id === product.id);
    if (index + direction < 0 || index + direction >= sameCategory.length) return;
    const reordered = [...sameCategory];
    reordered.splice(index, 1);
    reordered.splice(index + direction, 0, product);
    try {
      for (let i = 0; i < reordered.length; i++) {
        if (reordered[i].sortOrder !== i + 1) {
          await api(`/products/${reordered[i].id}`, {
            method: "PATCH",
            body: { sortOrder: i + 1, rowVersion: reordered[i].rowVersion },
          });
        }
      }
      await refreshProducts();
    } catch (err) {
      showError(err);
      await refreshProducts();
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
      <MerchantNav
        venueName={venue?.name}
        right={
          venues.length > 1 ? (
            <select
              value={venue?.id ?? ""}
              onChange={(e) => setVenue(venues.find((v) => v.id === e.target.value) ?? null)}
              className="max-w-32 rounded-lg border border-gray-300 px-2 py-1 text-xs sm:max-w-none sm:text-sm"
            >
              {venues.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.name}
                </option>
              ))}
            </select>
          ) : undefined
        }
      />

      {error && (
        <div className="mx-3 mt-3 rounded-lg bg-danger-bg px-4 py-2 text-sm text-danger sm:mx-6">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">
            ×
          </button>
        </div>
      )}

      <div className="mx-3 mt-3 flex flex-wrap items-center gap-2 rounded-2xl bg-white p-3 shadow sm:mx-6 sm:mt-4">
        <span className="text-sm font-semibold">Nhập dữ liệu nhanh:</span>
        <button
          onClick={() => venue && downloadAuthorizedFile(`/venues/${venue.id}/imports/template`, "TingGo_Import_Template.xlsx")}
          className="rounded-lg border border-orange-400 px-3 py-1.5 text-sm text-orange-600 hover:bg-orange-50"
        >
          ⬇ Tải file mẫu Excel
        </button>
        <label className="cursor-pointer rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-semibold text-white hover:bg-orange-700">
          {importing ? "Đang nhập..." : "⬆ Nhập từ Excel"}
          <input
            type="file"
            accept=".xlsx,.zip"
            className="hidden"
            disabled={importing}
            onChange={(e) => {
              const f = e.target.files?.[0];
              if (f) importFile(f);
              e.target.value = "";
            }}
          />
        </label>
        <span className="text-xs text-gray-400">
          .xlsx hoặc .zip (Excel + thư mục images/ chứa ảnh món) — xem trước rồi mới ghi
        </span>
      </div>

      {importSummary && (
        <div className="mx-3 mt-3 rounded-2xl bg-white p-4 text-sm shadow ring-1 ring-orange-200 sm:mx-6">
          <div className="flex items-center justify-between">
            <p className="font-semibold">
              Xem trước import — {importSummary.totalRows} dòng
              {importSummary.errorRows > 0 && (
                <span className="ml-2 text-red-600">{importSummary.errorRows} lỗi</span>
              )}
              {importSummary.warningRows > 0 && (
                <span className="ml-2 text-amber-600">{importSummary.warningRows} cảnh báo</span>
              )}
            </p>
            <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${
              importSummary.canCommit ? "bg-green-100 text-green-700" : "bg-red-100 text-red-700"
            }`}>
              {importSummary.canCommit ? "Sẵn sàng import" : "Cần sửa lỗi"}
            </span>
          </div>
          <p className="mt-1 text-gray-600">
            {importSummary.sections
              .map((s) => `${s.total - s.errors} ${SECTION_LABEL[s.section] ?? s.section}`)
              .join(" · ")}
          </p>
          {importIssues.length > 0 && (
            <ul className="mt-2 max-h-40 space-y-0.5 overflow-y-auto rounded-lg bg-gray-50 p-2 text-xs">
              {importIssues.map((issue, index) => (
                <li key={index} className={
                  issue.severity === "ERROR" ? "text-red-600" :
                  issue.severity === "WARNING" ? "text-amber-600" : "text-gray-500"
                }>
                  [{issue.severity}] {issue.sheetName} dòng {issue.rowNumber ?? "-"}: {issue.message}
                </li>
              ))}
            </ul>
          )}
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              onClick={commitImport}
              disabled={!importSummary.canCommit || committing}
              className="rounded-lg bg-orange-600 px-4 py-1.5 text-sm font-semibold text-white hover:bg-orange-700 disabled:opacity-40"
            >
              {committing ? "Đang import..." : "Xác nhận import"}
            </button>
            {(importSummary.errorRows > 0 || importSummary.warningRows > 0) && (
              <button
                onClick={() => downloadAuthorizedFile(`/imports/${importSummary.importId}/error-file`, "TingGo_Import_Errors.xlsx")}
                className="rounded-lg border border-amber-400 px-3 py-1.5 text-sm text-amber-700 hover:bg-amber-50"
              >
                ⬇ Tải file báo lỗi
              </button>
            )}
            <button onClick={cancelImport} className="rounded-lg border px-3 py-1.5 text-sm hover:bg-gray-50">
              Hủy
            </button>
          </div>
        </div>
      )}

      {commitOutcome && (
        <div className="mx-3 mt-3 rounded-2xl bg-green-50 p-4 text-sm shadow ring-1 ring-green-200 sm:mx-6">
          <p className="font-semibold text-green-800">✓ Import hoàn tất</p>
          <p className="mt-1">
            {commitOutcome.areasCreated} khu vực · {commitOutcome.tablesCreated} bàn (đã tạo QR) ·{" "}
            {commitOutcome.categoriesCreated} danh mục · {commitOutcome.productsCreated} món ·{" "}
            {commitOutcome.variantsCreated} size · {commitOutcome.groupsCreated} nhóm tùy chọn
          </p>
          <p className="mt-1 text-green-900">
            {commitOutcome.menuCreated
              ? "Menu đang ở trạng thái NHÁP — kiểm tra bên dưới rồi bấm nút công bố."
              : "Dữ liệu đã thêm vào menu hiện tại."}
            {" "}Sang <a href="/tables" className="font-semibold text-orange-600 underline">Bàn & QR</a> để in QR.
          </p>
          <button onClick={() => setCommitOutcome(null)} className="mt-2 text-xs text-gray-500 underline">Đóng</button>
        </div>
      )}

      <div className="grid gap-3 p-3 sm:gap-6 sm:p-6 lg:grid-cols-[300px_1fr]">
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
          <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
            <h2 className="font-semibold">Món ({products.length})</h2>
            {/* Form tìm món: lọc theo tên, không phân biệt dấu */}
            <div className="relative">
              <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-xs text-gray-400">
                🔍
              </span>
              <input
                type="search"
                value={productSearch}
                onChange={(e) => setProductSearch(e.target.value)}
                placeholder="Tìm món..."
                className="w-44 rounded-full border border-gray-300 bg-gray-50 py-1.5 pl-8 pr-7 text-sm focus:border-orange-400 focus:outline-none sm:w-56 [&::-webkit-search-cancel-button]:hidden"
              />
              {productSearch && (
                <button
                  onClick={() => setProductSearch("")}
                  aria-label="Xóa tìm kiếm"
                  className="absolute right-1.5 top-1/2 flex h-5 w-5 -translate-y-1/2 items-center justify-center rounded-full bg-gray-200 text-[10px] font-bold text-gray-600"
                >
                  ×
                </button>
              )}
            </div>
          </div>
          {menu && menu.categories.length > 0 && venue && (
            <ProductForm
              categories={menu.categories}
              venueId={venue.id}
              onCreated={refreshProducts}
              onError={showError}
            />
          )}
          <ul className="mt-4 grid gap-2 md:grid-cols-2">
            {visibleProducts.map((p) => (
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
                <div className="flex flex-col">
                  <button onClick={() => moveProduct(p, -1)} title="Lên"
                    className="text-xs leading-3 text-gray-400 hover:text-orange-600">▲</button>
                  <button onClick={() => moveProduct(p, 1)} title="Xuống"
                    className="text-xs leading-3 text-gray-400 hover:text-orange-600">▼</button>
                </div>
              </li>
            ))}
          </ul>
          {products.length === 0 && (
            <p className="py-8 text-center text-sm text-gray-400">
              Chưa có món nào. Tạo danh mục rồi thêm món đầu tiên nhé.
            </p>
          )}
          {products.length > 0 && visibleProducts.length === 0 && (
            <p className="py-8 text-center text-sm text-gray-400">
              Không tìm thấy món nào cho “{productSearch.trim()}”.
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
