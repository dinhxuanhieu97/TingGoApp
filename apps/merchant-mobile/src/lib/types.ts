export interface Membership {
  id: string;
  organizationId: string;
  venueId: string | null;
  role: string;
}

export interface Venue {
  id: string;
  name: string;
  slug: string;
  currencyCode: string;
}

export interface OrderItemView {
  productName: string;
  variantName?: string;
  quantity: number;
  unitPriceMinor: number;
  lineTotalMinor: number;
  note?: string;
  options: string[];
}

export interface OrderView {
  id: string;
  orderNumber: string;
  status: string;
  totalMinor: number;
  currencyCode: string;
  customerNote?: string;
  placedAt: string;
  rowVersion: number;
  items: OrderItemView[];
}

export interface ActiveOrder {
  view: OrderView;
  tableId?: string;
}

export interface Product {
  id: string;
  name: string;
  basePriceMinor: number;
  currencyCode: string;
  isAvailable: boolean;
}

export function formatMoney(minor: number, currency: string): string {
  return new Intl.NumberFormat("vi-VN", { style: "currency", currency }).format(
    currency === "VND" ? minor : minor / 100,
  );
}

export const NEXT_ACTION: Record<string, { action: string; label: string } | undefined> = {
  submitted: { action: "confirm", label: "Xác nhận" },
  confirmed: { action: "start-preparing", label: "Bắt đầu làm" },
  preparing: { action: "mark-ready", label: "Sẵn sàng" },
  ready: { action: "complete", label: "Hoàn thành" },
};

export const STATUS_LABEL: Record<string, string> = {
  submitted: "Đơn mới",
  confirmed: "Đã xác nhận",
  preparing: "Đang làm",
  ready: "Sẵn sàng",
};
