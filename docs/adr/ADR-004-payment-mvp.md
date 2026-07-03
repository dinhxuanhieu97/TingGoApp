# ADR-004: Thanh toán MVP — Cash + QR tĩnh, payOS lùi sang Commercial V1

**Status:** Accepted
**Date:** 2026-07-03
**Deciders:** Product Owner, Web Developer

## Context

PRD Sprint 8 dự kiến cash + manual bank transfer + payOS sandbox với webhook verification. Tuy nhiên webhook, retry, reconciliation là phần phức tạp và rủi ro cao trong khi KPI pilot không đo thanh toán online. Khách VN quen chuyển khoản qua QR ngân hàng (VietQR tĩnh).

## Decision

MVP chỉ hỗ trợ:
1. **Cash** — nhân viên xác nhận qua `/payments/{id}/confirm-cash`.
2. **QR tĩnh (VietQR)** — quán upload/khai báo QR ngân hàng; khách chuyển khoản; nhân viên đối chiếu app ngân hàng và xác nhận thủ công (dùng chung luồng confirm, `method=bank_transfer`).

**payOS lùi sang Commercial V1.** Giữ nguyên schema `payments` (`provider`, `provider_payment_id`) và bảng `payment_webhook_events` — không cần migration khi thêm payOS.

## Options Considered

### Option A: Cash + QR tĩnh ✅
**Pros:** bỏ toàn bộ webhook/signature/retry khỏi Sprint 8 (~1 tuần dev); phù hợp thói quen quán nhỏ; 0 phí giao dịch.
**Cons:** xác nhận thủ công — có thể sai sót đối chiếu; không tự động cập nhật `payment.paid` real-time.

### Option B: payOS sandbox + cash (theo PRD gốc)
**Pros:** sẵn sàng online payment khi launch; trạng thái paid tự động.
**Cons:** thêm webhook verification, idempotent event handling, sandbox↔production khác biệt; rủi ro trễ Sprint 8–9.

### Option C: Chỉ cash
**Pros:** tối giản nhất.
**Cons:** thiếu chuyển khoản — không thực tế với hành vi thanh toán VN hiện nay.

## Trade-off Analysis

Pilot cần chứng minh luồng **order** hoạt động, không phải luồng thanh toán online. QR tĩnh cho 90% giá trị (khách chuyển khoản được) với 10% chi phí (không cần tích hợp cổng). Thiết kế DB đã trung lập provider nên quyết định này không tạo nợ kiến trúc.

## Consequences

- Dễ hơn: Sprint 8 nhẹ đi rõ rệt → dồn lực cho push/TTS/hardening.
- Khó hơn: nhân viên phải đối chiếu chuyển khoản thủ công; báo cáo "doanh thu theo phương thức" dựa trên xác nhận tay.
- Revisit: Commercial V1 tích hợp payOS (webhook signature, `payment_webhook_events` đã có sẵn schema).

## Action Items

1. [ ] Thêm trường cấu hình QR ngân hàng vào venue settings (ảnh QR hoặc thông tin VietQR)
2. [ ] UI khách: hiển thị QR tĩnh + số tiền khi yêu cầu thanh toán
3. [ ] Luồng nhân viên xác nhận cash/bank_transfer + audit log
4. [ ] Đánh dấu các endpoint payOS trong OpenAPI là "reserved – Commercial V1"
