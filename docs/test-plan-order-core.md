# Test Plan — Sprint 5 Order Core

KPI cứng (PRD 2.5): **0% order trùng khi client gửi lại request**, **0% order bị mất sau khi backend xác nhận**.

## 1. Unit tests (nhanh, nhiều)

### Order state machine (bắt buộc 100% nhánh)

| # | From | Action | Kết quả |
|---|------|--------|---------|
| U1 | submitted | confirm | → confirmed |
| U2 | submitted | reject | → rejected (bắt buộc reason) |
| U3 | confirmed | start-preparing | → preparing |
| U4 | preparing | mark-ready | → ready |
| U5 | ready | complete | → completed |
| U6 | confirmed/preparing | cancel | → cancelled |
| U7 | completed/rejected/cancelled | mọi action | ORDER_INVALID_STATUS |
| U8 | submitted | mark-ready (nhảy cóc) | ORDER_INVALID_STATUS |

### Tính tiền snapshot
- U9: line_total = (base + variant_delta + Σoption_delta) × quantity — số học `long`, không float.
- U10: subtotal = Σline_total; total = subtotal − discount + tax.

## 2. Integration tests (chạm DB thật)

### Idempotency — KPI 0% trùng
- I1: Gửi 2 lần cùng `Idempotency-Key` + cùng body → 1 order duy nhất trong DB, response giống nhau.
- I2: Cùng key, body khác (request_hash khác) → 422 CONFLICT, không tạo order thứ hai.
- I3: Cùng `client_order_id` khác key → unique (venue_id, client_order_id) chặn, trả order đã có.
- I4: 2 request song song cùng key → chỉ 1 order (unique constraint là chốt chặn cuối).

### Price validation & snapshot
- I5: Client gửi productId của quán khác → VALIDATION_FAILED (tenant isolation).
- I6: Món `is_available=false` → PRODUCT_UNAVAILABLE, không tạo order.
- I7: Giá lưu trong order_items là snapshot server-side — client không gửi giá; đổi giá sản phẩm sau khi đặt không đổi order cũ.

### State transition qua API
- I8: confirm với `rowVersion` cũ → 409 ORDER_STALE_VERSION.
- I9: Mỗi transition ghi 1 dòng order_status_history (from, to, actor).
- I10: User quán khác gọi confirm → 403 (tenant isolation DoD).

### Outbox — KPI 0% mất order
- I11: Order tạo thành công ⇒ tồn tại outbox_event `order.created` cùng transaction (kiểm tra trong DB).
- I12: Transition ⇒ outbox_event tương ứng (`order.confirmed`...).

### Table session
- I13: 2 lần POST /public/table-sessions cùng bàn → cùng 1 session (open).
- I14: Order thứ hai cùng session → mã order riêng, chung session (CUS-07).

## 3. Không test (MVP)
- Load test song song lớn (Sprint 10), SignalR delivery (Sprint 6), payment (Sprint 8).

## Định nghĩa hoàn thành Sprint 5
Tất cả U1–U10, I1–I14 pass; `dotnet test` xanh; e2e browser: khách gửi order từ giỏ → thấy mã order.
