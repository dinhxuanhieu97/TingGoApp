"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership, Venue } from "@/lib/types";
import { formatMoney } from "@/lib/types";
import MerchantNav from "@/components/MerchantNav";

interface TodayReport {
  date: string;
  currencyCode: string;
  revenuePaidMinor: number;
  orderCount: number;
  orderTotalMinor: number;
  averageOrderMinor: number;
  rejectedOrCancelled: number;
  byPaymentMethod: { method: string; totalMinor: number; count: number }[];
}

interface TopProduct {
  productName: string;
  quantity: number;
  revenueMinor: number;
}

interface DailySales {
  date: string;
  orderCount: number;
  totalMinor: number;
}

export default function ReportsPage() {
  const router = useRouter();
  const [venue, setVenue] = useState<Venue | null>(null);
  const [today, setToday] = useState<TodayReport | null>(null);
  const [products, setProducts] = useState<TopProduct[]>([]);
  const [sales, setSales] = useState<DailySales[]>([]);
  const [error, setError] = useState("");

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
        const venues = await api<Venue[]>(`/organizations/${memberships[0].organizationId}/venues`);
        const selected = venues[0];
        if (!selected) return;
        setVenue(selected);
        const [todayData, productData, salesData] = await Promise.all([
          api<TodayReport>(`/venues/${selected.id}/reports/today`),
          api<TopProduct[]>(`/venues/${selected.id}/reports/products?days=7`),
          api<DailySales[]>(`/venues/${selected.id}/reports/sales?days=7`),
        ]);
        setToday(todayData);
        setProducts(productData);
        setSales(salesData);
      } catch (err) {
        setError(err instanceof ApiError ? err.message : "Không tải được báo cáo.");
      }
    })();
  }, [router]);

  const currency = today?.currencyCode ?? "VND";

  return (
    <main className="min-h-screen bg-orange-50">
      <MerchantNav venueName={venue?.name} />

      {error && (
        <div className="mx-6 mt-4 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">{error}</div>
      )}

      <div className="p-3 sm:p-6">
        {today && (
          <section className="mb-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <Card title="Doanh thu đã thu hôm nay" value={formatMoney(today.revenuePaidMinor, currency)} />
            <Card title="Số order" value={String(today.orderCount)} />
            <Card title="Giá trị TB / order" value={formatMoney(today.averageOrderMinor, currency)} />
            <Card title="Từ chối / hủy" value={String(today.rejectedOrCancelled)} />
          </section>
        )}

        <div className="grid gap-6 lg:grid-cols-2">
          <section className="rounded-2xl bg-white p-4 shadow">
            <h2 className="mb-3 font-semibold">Món bán chạy (7 ngày)</h2>
            <ul className="space-y-1">
              {products.map((product, index) => (
                <li key={product.productName} className="flex justify-between text-sm">
                  <span>{index + 1}. {product.productName}</span>
                  <span className="text-gray-500">
                    {product.quantity} món · {formatMoney(product.revenueMinor, currency)}
                  </span>
                </li>
              ))}
              {products.length === 0 && <p className="text-sm text-gray-400">Chưa có dữ liệu.</p>}
            </ul>
          </section>

          <section className="rounded-2xl bg-white p-4 shadow">
            <h2 className="mb-3 font-semibold">Doanh số theo ngày (7 ngày)</h2>
            <ul className="space-y-1">
              {sales.map((day) => (
                <li key={day.date} className="flex justify-between text-sm">
                  <span>{day.date}</span>
                  <span className="text-gray-500">
                    {day.orderCount} order · {formatMoney(day.totalMinor, currency)}
                  </span>
                </li>
              ))}
              {sales.length === 0 && <p className="text-sm text-gray-400">Chưa có dữ liệu.</p>}
            </ul>
            {today?.byPaymentMethod && today.byPaymentMethod.length > 0 && (
              <>
                <h3 className="mb-1 mt-4 text-sm font-semibold">Theo phương thức (hôm nay)</h3>
                <ul className="space-y-1 text-sm text-gray-600">
                  {today.byPaymentMethod.map((method) => (
                    <li key={method.method} className="flex justify-between">
                      <span>{method.method === "cash" ? "Tiền mặt" : "Chuyển khoản"}</span>
                      <span>{formatMoney(method.totalMinor, currency)}</span>
                    </li>
                  ))}
                </ul>
              </>
            )}
            {venue && (
              <a
                href={`${(process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1")}/venues/${venue.id}/reports/export?days=30`}
                className="mt-4 block text-center text-sm text-orange-600 hover:underline"
              >
                ⬇ Xuất CSV 30 ngày
              </a>
            )}
          </section>
        </div>
      </div>
    </main>
  );
}

function Card({ title, value }: { title: string; value: string }) {
  return (
    <div className="rounded-2xl bg-white p-4 shadow">
      <p className="text-xs text-gray-500">{title}</p>
      <p className="mt-1 text-xl font-bold text-orange-600">{value}</p>
    </div>
  );
}
