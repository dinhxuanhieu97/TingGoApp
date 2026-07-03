# TINGGO — KẾ HOẠCH PHÁT TRIỂN TỪNG BƯỚC (với Claude Cowork)

**Nguồn:** PRD.md v0.1 | **Ngày lập:** 2026-07-03
**Mục tiêu:** Thực thi roadmap 20 tuần (Sprint 0–10) bằng cách kết hợp các skill, agent và tool sẵn có trong Cowork, kèm đề xuất công cụ cần xây thêm (MCP server, skill riêng).

---

## 0. HIỆN TRẠNG

- Thư mục dự án chỉ có `PRD.md` → dự án greenfield, bắt đầu từ Sprint 0.
- PRD đã đầy đủ: ERD, danh sách API, SignalR events, roadmap 10 sprint, Definition of Done.
- 8 quyết định ở mục 13 PRD **chưa chốt** → phải xử lý trước khi viết code.

---

## 1. BẢN ĐỒ SKILL / AGENT / TOOL SẴN CÓ

| Nhu cầu | Công cụ Cowork | Dùng khi nào |
| --- | --- | --- |
| Ghi lại quyết định kiến trúc (ADR) | `engineering:architecture` | Sprint 0: chốt mobile framework, cloud, payOS, TTS |
| Thiết kế hệ thống, API, data model | `engineering:system-design` | Sprint 0: review ERD, order state machine, SignalR |
| Chiến lược test | `engineering:testing-strategy` | Sprint 0 & 5: test plan cho idempotency, state machine |
| Review code trước merge | `engineering:code-review` | Mọi sprint, mỗi PR quan trọng |
| Debug lỗi | `engineering:debug` | Khi có bug khó (reconnect, race condition) |
| Checklist trước deploy | `engineering:deploy-checklist` | Cuối mỗi sprint, trước pilot |
| Viết tài liệu, README, runbook | `engineering:documentation` | Sprint 1 (README), Sprint 9 (runbook restore/backup) |
| Kiểm soát nợ kỹ thuật | `engineering:tech-debt` | Sprint 9 hardening |
| Sự cố production khi pilot | `engineering:incident-response` | Sprint 10 & pilot |
| Standup hàng ngày | `engineering:standup` | Suốt dự án |
| Spec bàn giao design → dev | `design:design-handoff` | Sau khi có Figma (PRE-09) |
| Review UI/UX mockup | `design:design-critique` | Sprint 0–1, trước khi code UI |
| Audit accessibility | `design:accessibility-review` | Sprint 9, Customer Web |
| Microcopy, error message, empty state | `design:ux-copy` | Sprint 4–9 (đa ngôn ngữ vi/en) |
| Kế hoạch phỏng vấn quán pilot | `design:user-research` + `design:research-synthesis` | PRE-10 và Sprint 10 (feedback P0/P1/P2) |
| Xây MCP server | `mcp-builder` | Xem mục 3 |
| Tạo skill nội bộ cho team | `skill-creator` | Đóng gói convention TingGo thành skill |
| Poster QR, brand asset | `canvas-design` | Sprint 4 (QR poster template) |
| Báo cáo/xuất file | `xlsx`, `docx`, `pdf` | Báo cáo pilot, export mẫu |
| UI component tham khảo | Shadcn UI MCP (đã kết nối) | Merchant Web (Next.js) |
| Figma → code | Figma MCP (cần authorize) | Sprint 1+ khi có design |
| Nghiên cứu đối thủ | `marketing:competitive-brief` | Song song, chuẩn bị commercial launch |
| Kế hoạch launch | `marketing:campaign-plan` | Sau pilot |

**Connector nên kết nối thêm:** GitHub (repo, PR, CI), Linear/Jira (backlog 10 sprint), Slack (thông báo), Figma (design). Các connector này đã có trong plugin engineering/design nhưng cần authorize.

---

## 2. KẾ HOẠCH TỪNG BƯỚC

### Giai đoạn A — Trước khi code (tuần 1–2, Sprint 0)

**Bước A1. Chốt 8 quyết định mở (PRD mục 13)**
- Chạy `engineering:architecture` để tạo ADR cho từng quyết định:
  - ADR-001: Mobile — React Native vs Ionic/Capacitor
  - ADR-002: Cloud — AWS vs Azure (vs VN provider cho latency)
  - ADR-003: OTP — email vs SMS vs cả hai
  - ADR-004: Payment MVP — payOS vs chỉ cash/QR tĩnh
  - ADR-005: TTS trong MVP hay Premium sau pilot
- Claude có thể draft sẵn phân tích trade-off, team duyệt.

**Bước A2. Review thiết kế hệ thống**
- Chạy `engineering:system-design` với ERD + API list trong PRD để:
  - Kiểm tra order state machine (PRE-06)
  - Kiểm tra idempotency flow (CUS-05) và transactional outbox
  - Kiểm tra tenant isolation
- Output: permission matrix (PRE-08), state machine diagram đã duyệt.

**Bước A3. Setup hạ tầng làm việc**
- Tạo Git repo, cấu trúc monorepo (backend / customer-web / merchant-web / mobile).
- Kết nối GitHub connector để Claude review PR trực tiếp.
- Đưa backlog 10 sprint vào Linear/Jira (Claude tạo hàng loạt issue từ PRD).

**Bước A4. Design flow chính (PRE-09)**
- Nếu chưa có UI/UX: dùng `design:design-critique` + Shadcn UI MCP để Claude đề xuất wireframe.
- Khi có Figma: authorize Figma connector, dùng `design:design-handoff` để sinh spec.

**Bước A5. Tạo CLAUDE.md + skill nội bộ**
- Viết `CLAUDE.md` ở root repo: convention .NET, EF Core, error code format, quy tắc tiền BIGINT minor unit, UTC, row_version — để mọi phiên Claude code đúng chuẩn PRD.
- (Tùy chọn) `skill-creator`: đóng gói "TingGo API convention" thành skill để tái sử dụng.

### Giai đoạn B — Foundation (tuần 3–4, Sprint 1)

1. Scaffold ASP.NET Core solution (modular monolith), EF Core + PostgreSQL, Redis, Docker Compose — Claude viết trực tiếp trong thư mục dự án.
2. Global exception handling, validation, logging, OpenAPI — theo convention trong CLAUDE.md.
3. Next.js shell cho Customer Web và Merchant Web (Shadcn UI MCP hỗ trợ component).
4. Mobile project scaffold theo ADR-001.
5. CI/CD: Claude viết GitHub Actions; kiểm tra bằng `engineering:deploy-checklist`.
6. `engineering:documentation` → README + hướng dẫn dev environment.

### Giai đoạn C — Core Domain (tuần 5–12, Sprint 2–5)

Mỗi sprint lặp chu trình: **Claude implement → `engineering:code-review` → test → `engineering:standup` báo cáo**.

- **Sprint 2 — Auth & Venue:** OTP, refresh token rotation, membership, onboarding. Trước khi code auth chạy `engineering:testing-strategy` cho security test (OTP expiry, token rotation).
- **Sprint 3 — Menu:** CRUD menu/category/product/variant/modifier, image upload. `design:ux-copy` cho label và validation message vi/en.
- **Sprint 4 — Table & QR:** area, table, QR token (hash, không lưu raw), public menu. `canvas-design` tạo template poster QR đẹp cho quán.
- **Sprint 5 — Order Core (quan trọng nhất):** idempotency, snapshot, state machine, outbox. Bắt buộc:
  - `engineering:testing-strategy` → test plan cho 0% đơn trùng / 0% mất đơn (KPI PRD 2.5)
  - `engineering:code-review` kỹ transaction boundary
  - Unit test state machine đầy đủ trước khi merge.

### Giai đoạn D — Real-time & Vận hành (tuần 13–16, Sprint 6–7)

- SignalR OrderHub + Redis backplane; outbox worker; order board; reconnect snapshot API.
- Mobile: SignalR client, âm báo, resync (MOB-06). Bug reconnect khó → `engineering:debug`.
- Sprint 7: staff, role/permission, service request, bill, audit log. Verify permission matrix bằng integration test.

### Giai đoạn E — Push, Payment, Hardening (tuần 17–18, Sprint 8–9)

- Push notification, TTS (theo ADR-005), cash + payOS sandbox, webhook signature verification.
- Sprint 9: `engineering:tech-debt` audit; `design:accessibility-review` cho Customer Web; rate limiting, security review, backup/restore runbook (`engineering:documentation`).

### Giai đoạn F — QA & Pilot (tuần 19–20, Sprint 10)

- Integration + load test; `engineering:deploy-checklist` trước staging và production.
- Pilot 5 quán: `design:user-research` lập kế hoạch phỏng vấn; `design:research-synthesis` tổng hợp feedback thành P0/P1/P2.
- Sự cố khi pilot → `engineering:incident-response`.
- Báo cáo pilot: `xlsx` (KPI data) + `docx` (báo cáo) + `pptx` (present cho stakeholder).

---

## 3. ĐỀ XUẤT XÂY MỚI VỚI `mcp-builder`

Xây **TingGo MCP Server** — cầu nối để Claude (và sau này AI agent khác) thao tác trực tiếp với API TingGo. Xây sớm từ Sprint 5–6 khi API ổn định, dùng chính nó để test.

**Giai đoạn 1 (Sprint 5–6) — Dev/QA tooling:**
- `create_test_venue`, `seed_menu` — dựng dữ liệu test trong vài giây
- `submit_test_order` — bắn order giả lập để test real-time flow
- `get_active_orders`, `advance_order_status` — kiểm thử state machine
- Giá trị: giảm mạnh thời gian test integration, tự động hóa QA Sprint 10.

**Giai đoạn 2 (sau pilot) — Merchant assistant:**
- `update_product_availability`, `get_today_report`, `search_orders`
- Cho phép chủ quán hỏi Claude: "Hôm nay bán được bao nhiêu? Tắt món trà đào giúp tôi."
- Đây có thể là feature Premium khác biệt hóa với đối thủ.

**Cách làm:** chạy skill `/mcp-builder` → chọn TypeScript (MCP SDK) hoặc Python (FastMCP) → wrap các endpoint trong PRD mục 7 → auth bằng API key theo venue.

**Đề xuất skill nội bộ (dùng `skill-creator`):**
- `tinggo-api-convention` — enforce error format, idempotency, tenant isolation khi Claude viết endpoint mới
- `tinggo-migration-check` — checklist migration EF Core theo Definition of Done mục 12.

---

## 4. VIỆC CẦN LÀM NGAY (tuần này)

1. ~~Chốt ADR-001 → ADR-005~~ ✅ Đã tạo tại `docs/adr/` (2026-07-03): RN, AWS (dev local trước), OTP email, cash + QR tĩnh, TTS CapCut qua `ITtsEngine` (chờ chi tiết API).
2. Kết nối GitHub + Linear/Jira connector, tạo repo và import backlog.
3. Viết `CLAUDE.md` convention cho repo.
4. Xác nhận 5 quán pilot (PRE-10) — không phụ thuộc kỹ thuật, làm song song.
5. Bắt đầu Sprint 0 review với `engineering:system-design`.

---

## 5. NGUYÊN TẮC XUYÊN SUỐT

- Mỗi PR quan trọng qua `engineering:code-review` trước khi merge.
- Mỗi sprint kết thúc bằng `engineering:deploy-checklist` + demo trên staging.
- Definition of Done (PRD mục 12) là tiêu chuẩn nghiệm thu, không thỏa hiệp — đặc biệt: không lộ dữ liệu tenant khác, migration đã kiểm tra.
- KPI cứng: 0% order trùng, 0% mất order sau khi DB xác nhận, notification < 2s.
