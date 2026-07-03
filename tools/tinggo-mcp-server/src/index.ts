#!/usr/bin/env node
/**
 * TingGo MCP Server — dev/QA tooling (DEVELOPMENT_PLAN.md mục 3, Giai đoạn 1).
 * Wrap REST API local: seed dữ liệu test trong vài giây, bắn order giả lập,
 * kiểm thử state machine — thay cho thao tác tay lặp đi lặp lại.
 */
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { api, ensureLoggedIn, isLoggedIn, loginViaOtp, TingGoError, API_URL } from "./client.js";

const server = new McpServer({ name: "tinggo-mcp-server", version: "0.1.0" });

function ok(data: unknown) {
  return {
    content: [{ type: "text" as const, text: JSON.stringify(data, null, 2) }],
    structuredContent: (typeof data === "object" && data !== null && !Array.isArray(data)
      ? data
      : { items: data }) as Record<string, unknown>,
  };
}

function fail(error: unknown) {
  const message = error instanceof TingGoError
    ? `[${error.code}] ${error.message}`
    : error instanceof Error ? error.message : String(error);
  return { content: [{ type: "text" as const, text: `Lỗi: ${message}` }], isError: true };
}

// ---------- Auth ----------
server.registerTool(
  "tinggo_login",
  {
    title: "Đăng nhập TingGo (dev)",
    description:
      "Đăng nhập owner qua OTP email — mã OTP được đọc tự động từ Mailpit local. " +
      "Phải gọi trước các tool khác. Chỉ hoạt động với docker compose dev đang chạy.",
    inputSchema: {
      email: z.string().email().describe("Email owner, ví dụ dev@tinggo.local"),
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: true, openWorldHint: false },
  },
  async ({ email }) => {
    try {
      const user = await loginViaOtp(email);
      return ok({ loggedIn: true, ...user, apiUrl: API_URL });
    } catch (error) {
      return fail(error);
    }
  },
);

// ---------- Seed ----------
server.registerTool(
  "tinggo_seed_venue",
  {
    title: "Seed quán test hoàn chỉnh",
    description:
      "Tạo organization + venue + menu published (2 danh mục, 4 món, size + topping) + khu vực + N bàn có QR. " +
      "Trả về qrUrl từng bàn để test customer flow. Yêu cầu đã tinggo_login.",
    inputSchema: {
      name: z.string().min(1).max(200).default("Quán Test").describe("Tên quán"),
      tableCount: z.number().int().min(1).max(20).default(3).describe("Số bàn tạo kèm QR"),
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
  },
  async ({ name, tableCount }) => {
    try {
      await ensureLoggedIn();
      const suffix = Math.random().toString(36).slice(2, 6);
      const org = await api<{ id: string }>("/organizations", { body: { name: `${name} Org ${suffix}` } });
      const venue = await api<{ id: string; slug: string }>(`/organizations/${org.id}/venues`, {
        body: { name: `${name} ${suffix}` },
      });

      const menu = await api<{ id: string }>(`/venues/${venue.id}/menus`, { body: { name: "Menu chính" } });
      const drinks = await api<{ id: string }>(`/menus/${menu.id}/categories`, { body: { name: "Đồ uống" } });
      const food = await api<{ id: string }>(`/menus/${menu.id}/categories`, { body: { name: "Đồ ăn" } });

      const products: { id: string; name: string }[] = [];
      for (const spec of [
        { categoryId: drinks.id, name: "Cà phê sữa", basePriceMinor: 25000 },
        { categoryId: drinks.id, name: "Trà đào cam sả", basePriceMinor: 39000 },
        { categoryId: food.id, name: "Bánh mì chảo", basePriceMinor: 45000 },
        { categoryId: food.id, name: "Khoai tây chiên", basePriceMinor: 29000 },
      ]) {
        products.push(await api<{ id: string; name: string }>(`/venues/${venue.id}/products`, { body: spec }));
      }

      // Size cho món đầu + nhóm topping gán món thứ hai
      await api(`/products/${products[0].id}/variants`, {
        body: { name: "Size L", priceDeltaMinor: 6000, isDefault: false },
      });
      const group = await api<{ id: string }>(`/venues/${venue.id}/modifier-groups`, {
        body: { name: "Topping", minSelect: 0, maxSelect: 2, isRequired: false },
      });
      await api(`/modifier-groups/${group.id}/options`, { body: { name: "Trân châu", priceDeltaMinor: 7000 } });
      await api(`/modifier-groups/${group.id}/options`, { body: { name: "Thạch dừa", priceDeltaMinor: 5000 } });
      await api(`/products/${products[1].id}/modifier-groups`, {
        method: "PUT",
        body: { modifierGroupIds: [group.id] },
      });

      await api(`/menus/${menu.id}/publish`, { method: "POST", body: {} });

      const area = await api<{ id: string }>(`/venues/${venue.id}/areas`, { body: { name: "Tầng 1" } });
      const tables = await api<{ id: string; code: string; qrUrl: string; rawToken: string }[]>(
        `/venues/${venue.id}/tables/bulk`,
        { body: { areaId: area.id, count: tableCount } },
      );

      return ok({
        venueId: venue.id,
        slug: venue.slug,
        menuId: menu.id,
        products,
        tables: tables.map((t) => ({ id: t.id, code: t.code, qrUrl: t.qrUrl, qrToken: t.rawToken })),
      });
    } catch (error) {
      return fail(error);
    }
  },
);

// ---------- Order test ----------
server.registerTool(
  "tinggo_submit_test_order",
  {
    title: "Bắn order test (như khách quét QR)",
    description:
      "Giả lập khách: tạo/lấy phiên bàn từ qrToken rồi gửi order với Idempotency-Key. " +
      "Mặc định order 1 món đầu tiên trong menu; truyền items để tùy biến.",
    inputSchema: {
      qrToken: z.string().min(10).describe("Raw QR token của bàn (lấy từ tinggo_seed_venue)"),
      items: z
        .array(
          z.object({
            productId: z.string().uuid(),
            quantity: z.number().int().min(1).max(99).default(1),
            note: z.string().max(200).optional(),
          }),
        )
        .optional()
        .describe("Danh sách món; bỏ trống = tự chọn món đầu tiên của menu"),
      customerNote: z.string().max(500).optional(),
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
  },
  async ({ qrToken, items, customerNote }) => {
    try {
      const session = await api<{ sessionToken: string; table: { code: string } }>(
        "/public/table-sessions",
        { body: { qrToken }, auth: false },
      );

      let orderItems = items;
      if (!orderItems || orderItems.length === 0) {
        const qr = await api<{ venue: { slug: string } }>(`/public/q/${qrToken}`, { auth: false });
        const menu = await api<{ categories: { products: { id: string; isAvailable: boolean }[] }[] }>(
          `/public/venues/${qr.venue.slug}/menu`,
          { auth: false },
        );
        const firstProduct = menu.categories.flatMap((c) => c.products).find((p) => p.isAvailable);
        if (!firstProduct) throw new TingGoError("NO_PRODUCT", "Menu không có món khả dụng.", 400);
        orderItems = [{ productId: firstProduct.id, quantity: 1 }];
      }

      const order = await api<Record<string, unknown>>("/public/orders", {
        body: {
          sessionToken: session.sessionToken,
          clientOrderId: crypto.randomUUID(),
          items: orderItems.map((i) => ({ ...i, optionIds: [] })),
          customerNote,
        },
        headers: { "Idempotency-Key": crypto.randomUUID() },
        auth: false,
      });
      return ok({ table: session.table.code, sessionToken: session.sessionToken, order });
    } catch (error) {
      return fail(error);
    }
  },
);

server.registerTool(
  "tinggo_get_active_orders",
  {
    title: "Xem order đang hoạt động",
    description: "Danh sách order submitted/confirmed/preparing/ready của quán. Yêu cầu đã tinggo_login.",
    inputSchema: { venueId: z.string().uuid().describe("Venue id") },
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
  },
  async ({ venueId }) => {
    try {
      await ensureLoggedIn();
      const orders = await api(`/venues/${venueId}/orders/active`);
      return ok({ orders });
    } catch (error) {
      return fail(error);
    }
  },
);

server.registerTool(
  "tinggo_advance_order",
  {
    title: "Chuyển trạng thái order",
    description:
      "Thực hiện transition qua state machine: confirm | reject | start-preparing | mark-ready | complete | cancel. " +
      "RowVersion được tự lấy mới nhất. Reject/cancel cần reason. Yêu cầu đã tinggo_login.",
    inputSchema: {
      orderId: z.string().uuid(),
      action: z.enum(["confirm", "reject", "start-preparing", "mark-ready", "complete", "cancel"]),
      reason: z.string().max(500).optional().describe("Bắt buộc với reject"),
    },
    annotations: { readOnlyHint: false, destructiveHint: false, idempotentHint: false, openWorldHint: false },
  },
  async ({ orderId, action, reason }) => {
    try {
      await ensureLoggedIn();
      const current = await api<{ rowVersion: number }>(`/orders/${orderId}`);
      const order = await api(`/orders/${orderId}/${action}`, {
        body: { rowVersion: current.rowVersion, reason },
      });
      return ok({ order });
    } catch (error) {
      return fail(error);
    }
  },
);

server.registerTool(
  "tinggo_status",
  {
    title: "Trạng thái môi trường dev",
    description: "Kiểm tra API health, đã login chưa — gọi khi debug môi trường.",
    inputSchema: {},
    annotations: { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false },
  },
  async () => {
    try {
      const res = await fetch(`${API_URL.replace("/api/v1", "")}/health`);
      return ok({ apiUrl: API_URL, apiHealth: await res.text(), loggedIn: isLoggedIn() });
    } catch (error) {
      return fail(error);
    }
  },
);

const transport = new StdioServerTransport();
await server.connect(transport);
