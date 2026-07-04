# Security Review — TingGo (2026-07-04, trước pilot)

Rà theo PRD 5.3 + OWASP top items, phạm vi: backend + 2 web + mobile + docker + import.

## Đã vá trong đợt này

| # | Phát hiện | Mức | Xử lý |
|---|---|---|---|
| 1 | Postgres (5433), Redis (6379, **không mật khẩu**), Mailpit (8025, **chứa OTP**) bind `0.0.0.0` → mọi thiết bị Wi-Fi quán truy cập được khi chạy pilot | **Cao** | docker-compose bind `127.0.0.1` cả 3 service; docs pilot cập nhật |

## Đã kiểm tra — đạt

- **AuthN/Z**: JWT HS256 + refresh rotation + phát hiện token reuse (thu hồi toàn bộ phiên);
  staff PIN PBKDF2 100k; revoke nhân viên đăng xuất mọi thiết bị; thông báo login lỗi chung
  (chống enumeration); mọi endpoint nghiệp vụ kiểm tra membership theo organization/venue
  (tenant isolation có integration test).
- **Injection**: 100% EF Core parameterized; không raw SQL còn lại (đã refactor 2 chỗ cũ).
- **Upload**: ảnh validate MIME + magic bytes + ≤5MB; storage key ngẫu nhiên; ZIP chặn
  path traversal / nén lồng nhau / file thực thi (có test).
- **Idempotency + concurrency**: Idempotency-Key + unique constraints + rowVersion (có test race).
- **Rate limiting**: IP-based cho /public + /auth; OTP thêm 5/giờ/email; service request 3 pending/phiên.
- **Secrets**: không có secret thật trong repo; JWT secret dev có ghi chú đổi qua env;
  OTP/refresh/PIN/QR token chỉ lưu hash.
- **Error handling**: ApiError không lộ stack trace/SQL; lỗi 5xx chỉ log server.
- **CORS**: origin cụ thể (không wildcard) + AllowCredentials chỉ cho SignalR negotiate.

## Ghi nhận — chấp nhận ở pilot, xử lý khi lên production

| # | Vấn đề | Mức | Kế hoạch |
|---|---|---|---|
| 2 | JWT secret dev nằm trong appsettings.json | Trung bình | Khi deploy: env `Jwt__Secret` (đã hỗ trợ), secret ngẫu nhiên ≥64 bytes |
| 3 | Access token qua query string cho SignalR có thể lọt vào access log | Thấp | Production: bật HTTPS + không log query string; token sống 15 phút |
| 4 | HTTP (không TLS) trong LAN pilot | Trung bình | Chấp nhận ở pilot (mạng nội bộ quán); production bắt buộc HTTPS/ALB+ACM |
| 5 | Upload ảnh cho phép mọi staff (kể cả waiter) | Thấp | Cân nhắc giới hạn owner/manager khi có yêu cầu |
| 6 | Redis không auth (đã khóa localhost) | Thấp | Production: requirepass / ElastiCache auth |
| 7 | wifi_password_encrypted chưa dùng (chưa có tính năng hiện mật khẩu wifi) | Ghi chú | Khi làm: mã hóa bằng Data Protection API, không lưu plain |

Lần rà tiếp theo: trước khi mở internet-facing (AWS) — thêm dependency scan (`dotnet list
package --vulnerable`, `npm audit`) vào CI.
