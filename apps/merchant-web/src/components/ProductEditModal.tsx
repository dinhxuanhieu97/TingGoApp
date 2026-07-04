"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import type { Category, Product } from "@/lib/types";
import { formatMoney } from "@/lib/types";

interface Variant {
  id: string;
  name: string;
  priceDeltaMinor: number;
  isDefault: boolean;
  isAvailable: boolean;
}

interface ModifierOption {
  id: string;
  name: string;
  priceDeltaMinor: number;
}

interface ModifierGroup {
  id: string;
  name: string;
  minSelect: number;
  maxSelect: number;
  isRequired: boolean;
  options: ModifierOption[];
}

interface ProductDetail {
  product: Product;
  variants: Variant[];
  modifierGroupIds: string[];
}

interface Props {
  productId: string;
  venueId: string;
  categories: Category[];
  onClose: () => void;
  onSaved: () => Promise<void>;
}

export default function ProductEditModal({ productId, venueId, categories, onClose, onSaved }: Props) {
  const [detail, setDetail] = useState<ProductDetail | null>(null);
  const [groups, setGroups] = useState<ModifierGroup[]>([]);
  const [name, setName] = useState("");
  const [price, setPrice] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [assignedGroupIds, setAssignedGroupIds] = useState<string[]>([]);
  const [variantName, setVariantName] = useState("");
  const [variantDelta, setVariantDelta] = useState("");
  const [newGroupName, setNewGroupName] = useState("");
  const [newGroupOptions, setNewGroupOptions] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

  async function load() {
    const [detailData, groupData] = await Promise.all([
      api<ProductDetail>(`/products/${productId}`),
      api<ModifierGroup[]>(`/venues/${venueId}/modifier-groups`),
    ]);
    setDetail(detailData);
    setGroups(groupData);
    setName(detailData.product.name);
    setPrice(String(detailData.product.basePriceMinor));
    setDescription(detailData.product.description ?? "");
    setCategoryId(detailData.product.categoryId);
    setAssignedGroupIds(detailData.modifierGroupIds);
  }

  useEffect(() => {
    load().catch(showError);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [productId]);

  async function save() {
    if (!detail) return;
    setSaving(true);
    setError("");
    try {
      let imageUrl: string | undefined;
      if (file) {
        const formData = new FormData();
        formData.append("file", file);
        const uploaded = await api<{ url: string }>("/files/images", { formData });
        imageUrl = uploaded.url;
      }
      await api(`/products/${productId}`, {
        method: "PATCH",
        body: {
          name: name.trim(),
          basePriceMinor: Math.round(Number(price)),
          description: description.trim() || null,
          categoryId,
          imageUrl,
          rowVersion: detail.product.rowVersion,
        },
      });
      await api(`/products/${productId}/modifier-groups`, {
        method: "PUT",
        body: { modifierGroupIds: assignedGroupIds },
      });
      await onSaved();
      onClose();
    } catch (err) {
      showError(err);
      await load().catch(() => {}); // rowVersion cũ → nạp lại
    } finally {
      setSaving(false);
    }
  }

  async function addVariant() {
    if (!variantName.trim()) return;
    try {
      await api(`/products/${productId}/variants`, {
        body: {
          name: variantName.trim(),
          priceDeltaMinor: Math.round(Number(variantDelta || "0")),
          isDefault: detail?.variants.length === 0,
        },
      });
      setVariantName("");
      setVariantDelta("");
      await load();
    } catch (err) {
      showError(err);
    }
  }

  async function deleteVariant(variantId: string) {
    try {
      await api(`/product-variants/${variantId}`, { method: "DELETE" });
      await load();
    } catch (err) {
      showError(err);
    }
  }

  async function createGroup() {
    const options = newGroupOptions
      .split(";")
      .map((part) => part.trim())
      .filter(Boolean)
      .map((part) => {
        const [optionName, delta] = part.split(":").map((x) => x.trim());
        return { name: optionName, delta: Math.round(Number(delta || "0")) };
      })
      .filter((option) => option.name);
    if (!newGroupName.trim() || options.length === 0) {
      setError("Nhập tên nhóm và ít nhất 1 lựa chọn (VD: Trân châu:7000; Thạch:5000).");
      return;
    }
    try {
      const group = await api<{ id: string }>(`/venues/${venueId}/modifier-groups`, {
        body: { name: newGroupName.trim(), minSelect: 0, maxSelect: options.length, isRequired: false },
      });
      for (const option of options) {
        await api(`/modifier-groups/${group.id}/options`, {
          body: { name: option.name, priceDeltaMinor: option.delta },
        });
      }
      setNewGroupName("");
      setNewGroupOptions("");
      setAssignedGroupIds((prev) => [...prev, group.id]);
      await load();
    } catch (err) {
      showError(err);
    }
  }

  if (!detail) {
    return (
      <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/40">
        <div className="rounded-2xl bg-white p-6 text-sm text-gray-500">Đang tải...</div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 z-30 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-5"
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="mb-3 text-lg font-bold">Sửa món</h3>

        <label className="mb-2 block text-sm">
          <span className="font-medium">Tên món</span>
          <input value={name} onChange={(e) => setName(e.target.value)} maxLength={200}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none" />
        </label>
        <div className="mb-2 grid grid-cols-2 gap-2">
          <label className="block text-sm">
            <span className="font-medium">Giá (đồng)</span>
            <input type="number" min={0} value={price} onChange={(e) => setPrice(e.target.value)}
              className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none" />
          </label>
          <label className="block text-sm">
            <span className="font-medium">Danh mục</span>
            <select value={categoryId} onChange={(e) => setCategoryId(e.target.value)}
              className="mt-1 w-full rounded-lg border border-gray-300 px-2 py-2">
              {categories.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </label>
        </div>
        <label className="mb-2 block text-sm">
          <span className="font-medium">Mô tả ngắn</span>
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={2} maxLength={500}
            className="mt-1 w-full rounded-lg border border-gray-300 px-3 py-2 focus:border-orange-500 focus:outline-none" />
        </label>
        <label className="mb-3 block text-sm">
          <span className="font-medium">Đổi ảnh</span>
          <input type="file" accept="image/jpeg,image/png,image/webp"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            className="mt-1 block w-full text-xs" />
        </label>

        <div className="mb-3 rounded-xl bg-orange-50 p-3">
          <p className="mb-1 text-sm font-semibold">Size</p>
          {detail.variants.map((variant) => (
            <div key={variant.id} className="flex items-center justify-between py-0.5 text-sm">
              <span>
                {variant.name}
                {variant.priceDeltaMinor !== 0 &&
                  ` (${variant.priceDeltaMinor > 0 ? "+" : ""}${formatMoney(variant.priceDeltaMinor, detail.product.currencyCode)})`}
                {variant.isDefault && " ★"}
              </span>
              <button onClick={() => deleteVariant(variant.id)} className="text-xs text-red-500 hover:underline">Xóa</button>
            </div>
          ))}
          <div className="mt-1 flex gap-1">
            <input value={variantName} onChange={(e) => setVariantName(e.target.value)} placeholder="Tên (VD: L)"
              className="w-24 rounded-lg border border-gray-300 px-2 py-1 text-sm" />
            <input type="number" value={variantDelta} onChange={(e) => setVariantDelta(e.target.value)} placeholder="Phụ thu"
              className="flex-1 rounded-lg border border-gray-300 px-2 py-1 text-sm" />
            <button onClick={addVariant} className="rounded-lg bg-orange-600 px-3 text-sm font-semibold text-white">+</button>
          </div>
        </div>

        <div className="mb-3 rounded-xl bg-orange-50 p-3">
          <p className="mb-1 text-sm font-semibold">Nhóm topping / tùy chọn</p>
          {groups.map((group) => (
            <label key={group.id} className="flex items-center gap-2 py-0.5 text-sm">
              <input
                type="checkbox"
                checked={assignedGroupIds.includes(group.id)}
                onChange={(e) =>
                  setAssignedGroupIds((prev) =>
                    e.target.checked ? [...prev, group.id] : prev.filter((id) => id !== group.id),
                  )
                }
              />
              <span>
                {group.name}
                <span className="text-xs text-gray-400"> ({group.options.map((o) => o.name).join(", ")})</span>
              </span>
            </label>
          ))}
          <div className="mt-2 border-t border-orange-200 pt-2">
            <input value={newGroupName} onChange={(e) => setNewGroupName(e.target.value)} placeholder="Tên nhóm mới (VD: Topping)"
              className="mb-1 w-full rounded-lg border border-gray-300 px-2 py-1 text-sm" />
            <input value={newGroupOptions} onChange={(e) => setNewGroupOptions(e.target.value)}
              placeholder="Lựa chọn: Trân châu:7000; Thạch:5000"
              className="mb-1 w-full rounded-lg border border-gray-300 px-2 py-1 text-sm" />
            <button onClick={createGroup} className="rounded-lg border border-orange-400 px-3 py-1 text-xs font-semibold text-orange-600 hover:bg-orange-100">
              + Tạo nhóm & gán vào món
            </button>
          </div>
        </div>

        {error && <p className="mb-2 text-sm text-red-600">{error}</p>}

        <div className="flex gap-2">
          <button onClick={save} disabled={saving}
            className="flex-1 rounded-xl bg-orange-600 py-2.5 font-semibold text-white hover:bg-orange-700 disabled:opacity-50">
            {saving ? "Đang lưu..." : "Lưu"}
          </button>
          <button onClick={onClose} className="flex-1 rounded-xl border py-2.5 hover:bg-gray-50">Đóng</button>
        </div>

        {/* Xóa món = lưu trữ: món biến mất khỏi menu nhưng order cũ vẫn giữ snapshot */}
        <button
          onClick={async () => {
            if (!confirm(
              "Xóa món này khỏi menu? Món sẽ không hiện với khách nữa. " +
              "Các order đã đặt trước đó không bị ảnh hưởng.",
            )) return;
            try {
              await api(`/products/${productId}/archive`, { method: "POST" });
              await onSaved();
              onClose();
            } catch (err) {
              setError(err instanceof ApiError ? err.message : "Không xóa được món.");
            }
          }}
          className="mt-2 w-full rounded-xl py-2 text-sm font-medium text-danger hover:bg-red-50"
        >
          Xóa món khỏi menu
        </button>
      </div>
    </div>
  );
}
