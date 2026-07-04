# Hướng dẫn setup TingGo tại quán (pilot)

Dành cho người hỗ trợ pilot. Thời lượng mục tiêu: 30–60 phút/quán.

## Chuẩn bị trước khi đến quán

- [ ] MacBook đã cài: Docker Desktop, .NET 10, Node 22 (đã có sẵn trên máy dev)
- [ ] Kéo code mới nhất: `git pull`
- [ ] Sạc + adapter; giấy in poster QR (hoặc in trước ở nhà sau khi biết Wi-Fi quán? KHÔNG —
      QR phụ thuộc IP mạng quán, phải in tại quán)
- [ ] Điện thoại nhân viên cài sẵn **Expo Go** (App Store / Play Store)
- [ ] Xin trước của chủ quán: file menu (tên món, giá, mô tả) + ảnh món nếu có

## Tại quán — 8 bước

1. **Nối Wi-Fi quán** cho MacBook (mạng mà khách sẽ dùng). Tắt sleep:
   System Settings → Displays → Advanced → không sleep khi cắm điện.

2. **Chạy hệ thống:** `./scripts/start-pilot.sh` — ghi lại IP hiện ra (VD `192.168.1.50`).

3. **Tạo tài khoản quán:** mở `http://<IP>:3000` → nhập email chủ quán → đọc mã OTP tại
   `http://localhost:8025` **trên máy Mac** (đã khóa localhost vì bảo mật) → tạo tổ chức + quán. **BẤM GIỜ TỪ ĐÂY** (KPI < 10 phút).

4. **Import dữ liệu:** Menu → Tải file mẫu Excel → điền cùng chủ quán (hoặc dán từ file họ
   gửi trước) → nếu có ảnh: nén ZIP với thư mục `images/` → Nhập từ Excel → xem preview →
   Xác nhận → **Công bố menu**. DỪNG BẤM GIỜ khi order thử đầu tiên thành công (bước 7).

5. **In poster QR:** Bàn & QR → In poster tất cả bàn → in → dán bàn.

6. **Tạo nhân viên + cài app:** trang Nhân viên → thêm từng người (ghi mã NVxx + PIN đưa họ).
   Trên điện thoại nhân viên: mở terminal máy Mac →
   `cd apps/merchant-mobile && EXPO_PUBLIC_API_URL=http://<IP>:5080/api/v1 npx expo start`
   → quét QR Expo bằng Expo Go → đăng nhập tab Nhân viên (Venue ID copy từ trang Nhân viên).
   Bật TTS nếu quán muốn đọc đơn thành tiếng.

7. **Order thử trọn vòng (bắt buộc trước khi khách thật dùng):** điện thoại của bạn quét QR
   bàn thật → gọi 2 món có size/topping → nghe "ting" trên app nhân viên → xác nhận → làm →
   sẵn sàng → hoàn thành → khách bấm Thanh toán (kiểm tra QR ngân hàng hiện nếu đã cấu hình
   ở Cài đặt) → thu tiền → đóng bàn → xem Báo cáo có số liệu.

8. **Bàn giao:** chỉ chủ quán 3 thao tác họ dùng nhiều nhất — xem board Order, bật/tắt món
   hết hàng (app mobile tab Món), đóng bàn thu tiền. Dán giấy note: IP quản lý + số điện
   thoại hỗ trợ của bạn.

## Sự cố nhanh

| Triệu chứng | Xử lý |
|---|---|
| Khách quét QR không mở được | Điện thoại khách có cùng Wi-Fi quán không? IP đổi? → chạy lại script + in lại QR |
| App nhân viên "Đã xảy ra lỗi" | Expo chạy thiếu `EXPO_PUBLIC_API_URL`? → chạy lại lệnh bước 6 |
| Không thấy OTP | Mở `http://localhost:8025` **trên máy Mac** (đã khóa localhost vì bảo mật) trên máy Mac |
| Đơn không "ting" | Xem chấm Real-time trên board; mất mạng → tự reconnect + resync |
| Mất điện/sập máy | Mở lại → `./scripts/start-pilot.sh` — dữ liệu còn nguyên (Postgres volume) |

## Cuối buổi/cuối đợt

- Xuất CSV: Báo cáo → Xuất CSV 30 ngày
- Điền nhật ký pilot (docs/pilot-plan.md) + phỏng vấn theo mẫu
- `./scripts/stop-pilot.sh` nếu quán ngừng; backup: xem docs/runbook-backup-restore.md
