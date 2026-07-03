# ADR-003: Kênh OTP — Email

**Status:** Accepted
**Date:** 2026-07-03
**Deciders:** Web Developer, Product Owner

## Context

Chủ quán đăng nhập bằng OTP hoặc magic link (MOB-01); nhân viên dùng staff code + PIN nên không ảnh hưởng. PRD yêu cầu OTP expiration, rate limiting, không hard-code giả định số điện thoại VN (NFR 5.4). SMS tại VN cần đăng ký brandname, tốn phí mỗi tin và thủ tục chậm — không phù hợp timeline MVP.

## Decision

Dùng **OTP qua email** cho MVP. Schema `users` đã có `phone_e164` nullable — giữ nguyên để bổ sung SMS ở giai đoạn thương mại nếu pilot cho thấy chủ quán không quen email.

## Options Considered

### Option A: Email ✅
**Pros:** miễn phí/rẻ (SES ~$0.10/1000 mail), triển khai 1–2 ngày, quốc tế hóa sẵn, không thủ tục pháp lý.
**Cons:** một số chủ quán nhỏ ít dùng email; mail có thể vào spam (cần cấu hình SPF/DKIM).

### Option B: SMS
**Pros:** quen thuộc nhất với chủ quán VN.
**Cons:** brandname registration mất nhiều tuần; ~500–800đ/tin; mỗi quốc gia một nhà cung cấp — ngược mục tiêu i18n.

### Option C: Cả hai
**Pros:** linh hoạt tối đa.
**Cons:** gấp đôi công triển khai và test cho MVP; chưa có dữ liệu chứng minh cần.

## Trade-off Analysis

MVP cần tốc độ và chi phí thấp. Email đáp ứng cả hai và không chặn kiến trúc: bảng `users`, luồng `/auth/otp/request|verify` không phụ thuộc kênh gửi — thêm SMS provider sau chỉ là một implementation của `IOtpSender`.

## Consequences

- Dễ hơn: ship auth trong Sprint 2, không chi phí biến đổi.
- Khó hơn: onboarding quán mà chủ không có email — PM cần hỗ trợ tạo email khi pilot (ghi nhận làm dữ liệu quyết định SMS).
- Revisit: sau pilot, nếu >30% chủ quán gặp khó với email → ưu tiên SMS ở Commercial V1.

## Action Items

1. [ ] Interface `IOtpSender` + implementation email (SES local: MailHog/Mailpit khi dev)
2. [ ] Cấu hình SPF/DKIM/DMARC khi lên AWS
3. [ ] Rate limit: tối đa 5 OTP/giờ/email; OTP hết hạn 5 phút; một lần dùng
