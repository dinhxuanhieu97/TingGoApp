# TingGo — Hướng dẫn cho Claude

QR ordering platform cho quán ăn/cafe. PRD đầy đủ tại `PRD.md`, kế hoạch tại `DEVELOPMENT_PLAN.md`, quyết định kiến trúc tại `docs/adr/`.

## Stack (đã chốt — không đổi nếu không có ADR mới)

- Backend: C# / .NET 10, ASP.NET Core, **modular monolith** (`backend/`)
- DB: PostgreSQL + EF Core | Cache/backplane: Redis | Real-time: SignalR
- Customer Web & Merchant Web: Next.js + TypeScript (`apps/`)
- Mobile: React Native + TypeScript (ADR-001)
- Dev: 100% local qua Docker Compose; AWS chỉ từ Sprint 9 (ADR-002)
- OTP: email (ADR-003) | Payment MVP: cash + QR tĩnh, KHÔNG payOS (ADR-004)
- TTS: qua interface `ITtsEngine`, fallback on-device (ADR-005)

## Quy tắc dữ liệu (bắt buộc — từ PRD mục 6.1)

- Primary key: **UUID** (`Guid`, sinh v7 nếu có thể).
- Tiền: **BIGINT minor unit** (`long`, field suffix `_minor` / `Minor`). KHÔNG BAO GIỜ dùng float/double/decimal để lưu tiền.
- Thời gian: **UTC**, cột `TIMESTAMPTZ`, dùng `DateTimeOffset`/`timestamptz`. Bảng nghiệp vụ có `created_at`; bảng có sửa đổi thêm `updated_at`.
- Concurrency: cột `row_version` (BIGINT), client gửi `rowVersion` khi chuyển trạng thái order.
- `orders`, `payments`: KHÔNG xóa vật lý.
- `order_items` lưu **snapshot** tên/giá, không chỉ FK.
- Tenant isolation: mọi query nghiệp vụ PHẢI filter theo `venue_id` (và `organization_id` khi áp dụng). Không được để lộ dữ liệu tenant khác — đây là tiêu chí DoD.

## Quy tắc API (PRD mục 7.1)

- Base: `/api/v1`. Public routes: `/api/v1/public/...` (không auth).
- Headers: `Authorization: Bearer`, `Idempotency-Key` (bắt buộc với public order write), `Accept-Language`, `X-Venue-Id`.
- Error response thống nhất:
  ```json
  { "code": "ORDER_INVALID_STATUS", "message": "...", "traceId": "...", "details": {} }
  ```
  Error code: SCREAMING_SNAKE, prefix theo domain (`ORDER_`, `AUTH_`, `MENU_`, `PAYMENT_`...). Định nghĩa tại `TingGo.SharedKernel/ErrorCodes.cs`.
- Order submit: idempotent theo `(scope, Idempotency-Key)` + unique `(venue_id, client_order_id)`.
- Chuyển trạng thái order đi qua state machine tập trung — không set status trực tiếp.

## Quốc tế hóa (PRD 5.4)

KHÔNG hard-code: VNĐ, định dạng ngày VN, số điện thoại VN, text tiếng Việt trong backend, thuế suất. Mọi format theo `venue.default_locale`, `currency_code`, `timezone`.

## Cấu trúc backend

```
backend/
  TingGo.slnx
  src/
    TingGo.Api/            # Host: DI, middleware, SignalR hubs, OpenAPI
    TingGo.SharedKernel/   # ErrorCodes, ApiError, base types, abstractions
    TingGo.Infrastructure/ # DbContext, EF configs, Redis, outbox worker
    TingGo.Modules.Identity/       # users, auth OTP, memberships, sessions
    TingGo.Modules.Venues/         # organizations, venues, areas, tables, QR
    TingGo.Modules.Catalog/        # menus, categories, products, modifiers
    TingGo.Modules.Ordering/       # table sessions, orders, service requests
    TingGo.Modules.Payments/       # payments (cash, bank_transfer)
    TingGo.Modules.Notifications/  # device tokens, push, SignalR events
  tests/
    TingGo.UnitTests/
    TingGo.IntegrationTests/
```

Module được tham chiếu `SharedKernel` + `Infrastructure` (để dùng `TingGoDbContext`). Module KHÔNG tham chiếu module khác trực tiếp — giao tiếp qua contracts trong `SharedKernel/Contracts` (ví dụ `IMembershipService`). Entity của module đăng ký vào DbContext qua `IModuleEntityConfigurator`.

## Definition of Done (PRD mục 12 — rút gọn)

Code review, build pass, có validation + error handling + logging, unit test cho business rule quan trọng, integration test nếu chạm DB/external, UI xử lý loading/empty/error, migration đã kiểm tra, không lộ dữ liệu tenant khác.

## Lệnh thường dùng

```bash
docker compose up -d          # postgres, redis, mailpit
cd backend && dotnet build
dotnet test
dotnet run --project src/TingGo.Api
```
