export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  userId: string;
  email?: string;
}

export interface Membership {
  id: string;
  organizationId: string;
  venueId: string | null;
  role: string;
}

export interface Venue {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  currencyCode: string;
  defaultLocale: string;
  status: string;
  joinCode?: string;
  rowVersion: number;
}

export interface Menu {
  id: string;
  venueId: string;
  name: string;
  status: "draft" | "published";
  publishedAt?: string;
}

export interface Category {
  id: string;
  menuId: string;
  name: string;
  sortOrder: number;
  isVisible: boolean;
}

export interface Product {
  id: string;
  venueId: string;
  categoryId: string;
  name: string;
  description?: string;
  basePriceMinor: number;
  currencyCode: string;
  imageUrl?: string;
  status: string;
  isAvailable: boolean;
  sortOrder: number;
  rowVersion: number;
}

export function formatMoney(minor: number, currency: string, locale = "vi-VN"): string {
  // VND minor unit = đồng (0 số lẻ); các currency khác dùng Intl mặc định
  return new Intl.NumberFormat(locale, { style: "currency", currency }).format(
    currency === "VND" ? minor : minor / 100,
  );
}
