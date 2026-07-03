# TingGo MCP Server (dev/QA tooling)

MCP server cho phép Claude (Claude Code, Cowork, Desktop) thao tác trực tiếp với TingGo API local — seed dữ liệu test trong vài giây thay vì thao tác tay.

## Tools

| Tool | Chức năng |
|------|-----------|
| `tinggo_status` | Kiểm tra API health + trạng thái login |
| `tinggo_login` | Đăng nhập owner — OTP đọc tự động từ Mailpit (chỉ dev) |
| `tinggo_seed_venue` | Tạo org + venue + menu published (4 món, size, topping) + N bàn kèm QR |
| `tinggo_submit_test_order` | Giả lập khách quét QR và gửi order (idempotent) |
| `tinggo_get_active_orders` | Order đang hoạt động của quán |
| `tinggo_advance_order` | Chuyển trạng thái qua state machine (tự lấy rowVersion) |

## Setup

```bash
docker compose up -d                      # postgres, redis, mailpit
dotnet run --project backend/src/TingGo.Api
cd tools/tinggo-mcp-server && npm install --include=dev && npm run build
```

Repo đã có `.mcp.json` — mở Claude Code tại root repo là tools tự xuất hiện.

## Ví dụ dùng với Claude

> "Seed một quán 5 bàn rồi bắn 3 order vào bàn T01, confirm order đầu tiên"

Claude sẽ gọi `tinggo_login` → `tinggo_seed_venue` → `tinggo_submit_test_order` ×3 → `tinggo_advance_order`.

## Giai đoạn 2 (sau pilot)

Merchant assistant: `update_product_availability`, `get_today_report`, `search_orders` — cho chủ quán hỏi Claude "hôm nay bán được bao nhiêu?". Xem DEVELOPMENT_PLAN.md mục 3.
