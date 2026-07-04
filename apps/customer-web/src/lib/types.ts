export interface QrContext {
  venue: {
    id: string;
    name: string;
    slug: string;
    currencyCode: string;
    defaultLocale: string;
    wifiName?: string;
    bankQrImageUrl?: string;
    paymentMethods: string[];
  };
  table: { id: string; code: string; name: string };
  area: { id: string; name: string } | null;
}

export interface PublicVariant {
  id: string;
  name: string;
  priceDeltaMinor: number;
  isDefault: boolean;
}

export interface PublicModifierOption {
  id: string;
  name: string;
  priceDeltaMinor: number;
}

export interface PublicModifierGroup {
  id: string;
  name: string;
  minSelect: number;
  maxSelect: number;
  isRequired: boolean;
  options: PublicModifierOption[];
}

export interface PublicProduct {
  id: string;
  name: string;
  description?: string;
  basePriceMinor: number;
  currencyCode: string;
  imageUrl?: string;
  isAvailable: boolean;
  variants: PublicVariant[];
  modifierGroups: PublicModifierGroup[];
}

export interface PublicMenu {
  venue: { id: string; name: string; slug: string; currencyCode: string; defaultLocale: string };
  menu: { id: string; name: string };
  categories: { id: string; name: string; products: PublicProduct[] }[];
}

export interface CartItem {
  key: string;
  productId: string;
  productName: string;
  variantId?: string;
  variantName?: string;
  optionIds: string[];
  optionNames: string[];
  note?: string;
  quantity: number;
  unitPriceMinor: number;
}

export function cartTotal(items: CartItem[]): number {
  return items.reduce((sum, i) => sum + i.unitPriceMinor * i.quantity, 0);
}
