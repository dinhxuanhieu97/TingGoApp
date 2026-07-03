"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { HubConnectionBuilder, HubConnection, LogLevel } from "@microsoft/signalr";
import { api, ApiError, getTokens } from "@/lib/api";
import type { Membership, Venue } from "@/lib/types";
import { formatMoney } from "@/lib/types";

interface OrderView {
  id: string;
  orderNumber: string;
  status: string;
  totalMinor: number;
  currencyCode: string;
  customerNote?: string;
  placedAt: string;
  rowVersion: number;
  items: { productName: string; variantName?: string; quantity: number; note?: string; options: string[] }[];
}

interface ActiveOrder {
  view: OrderView;
  tableId?: string;
}

interface ServiceRequestItem {
  id: string;
  type: string;
  note?: string;
  status: string;
  requestedAt: string;
  tableId: string;
}

const SERVICE_TYPE_LABEL: Record<string, string> = {
  call_staff: "🔔 Gọi nhân viên",
  supplies: "🥢 Xin thêm đồ",
  payment: "💰 Thanh toán",
};

const COLUMNS: { status: string; title: string; actions: { action: string; label: string; danger?: boolean }[] }[] = [
  {
    status: "submitted",
    title: "Đơn mới",
    actions: [
      { action: "confirm", label: "Xác nhận" },
      { action: "reject", label: "Từ chối", danger: true },
    ],
  },
  { status: "confirmed", title: "Đã xác nhận", actions: [{ action: "start-preparing", label: "Bắt đầu làm" }] },
  { status: "preparing", title: "Đang làm", actions: [{ action: "mark-ready", label: "Sẵn sàng" }] },
  { status: "ready", title: "Sẵn sàng", actions: [{ action: "complete", label: "Hoàn thành" }] },
];

function beep() {
  try {
    const ctx = new AudioContext();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.frequency.value = 880;
    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);
    osc.start();
    osc.stop(ctx.currentTime + 0.4);
  } catch {
    /* trình duyệt chặn autoplay — bỏ qua */
  }
}

export default function OrdersPage() {
  const router = useRouter();
  const [venue, setVenue] = useState<Venue | null>(null);
  const [orders, setOrders] = useState<ActiveOrder[]>([]);
  const [tableCodes, setTableCodes] = useState<Record<string, string>>({});
  const [connected, setConnected] = useState(false);
  const [error, setError] = useState("");
  const connectionRef = useRef<HubConnection | null>(null);
  const seenEventIds = useRef<Set<string>>(new Set());

  const showError = (err: unknown) =>
    setError(err instanceof ApiError ? err.message : "Đã xảy ra lỗi.");

  const [serviceRequests, setServiceRequests] = useState<ServiceRequestItem[]>([]);

  const loadOrders = useCallback(async (venueId: string) => {
    setOrders(await api<ActiveOrder[]>(`/venues/${venueId}/orders/active`));
  }, []);

  const loadServiceRequests = useCallback(async (venueId: string) => {
    setServiceRequests(await api<ServiceRequestItem[]>(`/venues/${venueId}/service-requests`));
  }, []);

  // Khởi tạo: venue + orders + bảng mã bàn
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
        const tables = await api<{ id: string; code: string }[]>(`/venues/${selected.id}/tables`);
        setTableCodes(Object.fromEntries(tables.map((t) => [t.id, t.code])));
        await Promise.all([loadOrders(selected.id), loadServiceRequests(selected.id)]);
      } catch (err) {
        showError(err);
      }
    })();
  }, [router, loadOrders]);

  // SignalR: nhận order.* real-time (PRD mục 8)
  useEffect(() => {
    if (!venue) return;
    const apiOrigin = (process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080/api/v1")
      .replace("/api/v1", "");
    const connection = new HubConnectionBuilder()
      .withUrl(`${apiOrigin}/hubs/orders`, {
        accessTokenFactory: () => getTokens()?.accessToken ?? "",
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const events = [
      "order.created", "order.confirmed", "order.rejected",
      "order.preparing", "order.ready", "order.completed", "order.cancelled",
    ];
    for (const eventType of events) {
      connection.on(eventType, (payload: { eventId?: string }) => {
        // Chống xử lý lặp sự kiện (PRD 8.4)
        if (payload.eventId) {
          if (seenEventIds.current.has(payload.eventId)) return;
          seenEventIds.current.add(payload.eventId);
        }
        if (eventType === "order.created") beep();
        loadOrders(venue.id).catch(() => {});
      });
    }
    for (const eventType of [
      "service_request.created", "service_request.acknowledged",
      "service_request.resolved", "service_request.cancelled",
    ]) {
      connection.on(eventType, (payload: { eventId?: string }) => {
        if (payload.eventId) {
          if (seenEventIds.current.has(payload.eventId)) return;
          seenEventIds.current.add(payload.eventId);
        }
        if (eventType === "service_request.created") beep();
        loadServiceRequests(venue.id).catch(() => {});
      });
    }

    connection
      .start()
      .then(() => connection.invoke("JoinVenue", venue.id))
      .then(() => setConnected(true))
      .catch((err) => setError(`SignalR: ${err.message ?? err}`));
    connection.onreconnected(() => {
      setConnected(true);
      connection.invoke("JoinVenue", venue.id).catch(() => {});
      loadOrders(venue.id).catch(() => {}); // resync snapshot (MOB-06 pattern)
    });
    connection.onclose(() => setConnected(false));
    connectionRef.current = connection;
    return () => {
      connection.stop();
    };
  }, [venue, loadOrders]);

  async function advance(order: OrderView, action: string) {
    try {
      const reason = action === "reject" ? prompt("Lý do từ chối:") : undefined;
      if (action === "reject" && !reason) return;
      await api(`/orders/${order.id}/${action}`, {
        body: { rowVersion: order.rowVersion, reason },
      });
      if (venue) await loadOrders(venue.id);
    } catch (err) {
      showError(err);
      if (venue) await loadOrders(venue.id); // rowVersion stale → tải lại
    }
  }

  return (
    <main className="min-h-screen bg-orange-50">
      <header className="flex items-center justify-between border-b bg-white px-6 py-3">
        <div className="flex items-center gap-4">
          <span className="text-xl font-bold text-orange-600">TingGo</span>
          <nav className="flex gap-3 text-sm">
            <a href="/menu" className="text-gray-500 hover:text-orange-600">Menu</a>
            <a href="/tables" className="text-gray-500 hover:text-orange-600">Bàn & QR</a>
            <span className="font-semibold text-orange-600">Order</span>
          </nav>
        </div>
        <div className="flex items-center gap-3 text-sm">
          <span className={`flex items-center gap-1 ${connected ? "text-green-600" : "text-gray-400"}`}>
            <span className={`h-2 w-2 rounded-full ${connected ? "bg-green-500" : "bg-gray-400"}`} />
            {connected ? "Real-time" : "Đang kết nối..."}
          </span>
          <span className="text-gray-500">{venue?.name}</span>
        </div>
      </header>

      {error && (
        <div className="mx-6 mt-4 rounded-lg bg-red-100 px-4 py-2 text-sm text-red-700">
          {error}
          <button onClick={() => setError("")} className="ml-2 font-bold">×</button>
        </div>
      )}

      {serviceRequests.length > 0 && (
        <div className="mx-6 mt-4 rounded-2xl bg-amber-50 p-3 shadow ring-1 ring-amber-200">
          <h2 className="mb-2 text-sm font-semibold text-amber-800">
            Yêu cầu từ khách ({serviceRequests.length})
          </h2>
          <ul className="flex flex-wrap gap-2">
            {serviceRequests.map((request) => (
              <li key={request.id} className="flex items-center gap-2 rounded-xl bg-white px-3 py-2 text-sm shadow-sm">
                <span className="font-semibold">Bàn {tableCodes[request.tableId] ?? "?"}</span>
                <span>{SERVICE_TYPE_LABEL[request.type] ?? request.type}</span>
                {request.note && <span className="text-xs text-gray-500">“{request.note}”</span>}
                {request.status === "pending" ? (
                  <button
                    onClick={async () => {
                      try {
                        await api(`/service-requests/${request.id}/acknowledge`, { method: "POST" });
                        if (venue) await loadServiceRequests(venue.id);
                      } catch (err) { showError(err); }
                    }}
                    className="rounded-lg bg-amber-500 px-2 py-1 text-xs font-semibold text-white hover:bg-amber-600"
                  >
                    Đã nhận
                  </button>
                ) : (
                  <button
                    onClick={async () => {
                      try {
                        await api(`/service-requests/${request.id}/resolve`, { method: "POST" });
                        if (venue) await loadServiceRequests(venue.id);
                      } catch (err) { showError(err); }
                    }}
                    className="rounded-lg bg-green-600 px-2 py-1 text-xs font-semibold text-white hover:bg-green-700"
                  >
                    Xong
                  </button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="grid gap-4 p-6 md:grid-cols-2 xl:grid-cols-4">
        {COLUMNS.map((column) => {
          const columnOrders = orders.filter((o) => o.view.status === column.status);
          return (
            <section key={column.status} className="rounded-2xl bg-white p-3 shadow">
              <h2 className="mb-2 flex items-center justify-between font-semibold">
                {column.title}
                <span className="rounded-full bg-orange-100 px-2 py-0.5 text-xs text-orange-700">
                  {columnOrders.length}
                </span>
              </h2>
              <ul className="space-y-2">
                {columnOrders.map(({ view: order, tableId }) => (
                  <li key={order.id} className="rounded-xl border p-3">
                    <div className="flex items-center justify-between">
                      <span className="font-bold">{order.orderNumber}</span>
                      <span className="rounded bg-gray-100 px-2 py-0.5 text-xs font-semibold">
                        Bàn {tableId ? (tableCodes[tableId] ?? "?") : "?"}
                      </span>
                    </div>
                    <p className="mt-1 text-xs text-gray-400">
                      {new Date(order.placedAt).toLocaleTimeString("vi-VN")} ·{" "}
                      {formatMoney(order.totalMinor, order.currencyCode)}
                    </p>
                    <ul className="mt-1 text-sm">
                      {order.items.map((item, index) => (
                        <li key={index}>
                          {item.quantity}× {item.productName}
                          {item.variantName ? ` (${item.variantName})` : ""}
                          {item.options.length > 0 && (
                            <span className="text-xs text-gray-500"> — {item.options.join(", ")}</span>
                          )}
                          {item.note && <span className="text-xs italic text-gray-400"> “{item.note}”</span>}
                        </li>
                      ))}
                    </ul>
                    {order.customerNote && (
                      <p className="mt-1 text-xs italic text-orange-700">Ghi chú: {order.customerNote}</p>
                    )}
                    <div className="mt-2 flex gap-2">
                      {column.actions.map(({ action, label, danger }) => (
                        <button
                          key={action}
                          onClick={() => advance(order, action)}
                          className={`flex-1 rounded-lg py-1.5 text-xs font-semibold text-white ${
                            danger ? "bg-red-500 hover:bg-red-600" : "bg-orange-600 hover:bg-orange-700"
                          }`}
                        >
                          {label}
                        </button>
                      ))}
                    </div>
                  </li>
                ))}
              </ul>
              {columnOrders.length === 0 && (
                <p className="py-6 text-center text-xs text-gray-300">Trống</p>
              )}
            </section>
          );
        })}
      </div>
    </main>
  );
}
