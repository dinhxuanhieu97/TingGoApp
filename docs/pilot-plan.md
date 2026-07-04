# Kế hoạch Pilot 5 quán (PRD Sprint 10 / PRE-10)

**Mục tiêu:** chứng minh quy trình PRD 2.4 chạy trọn vẹn tại quán thật, đo KPI, thu feedback
phân loại P0/P1/P2 để chốt backlog thương mại.

## Mô hình triển khai pilot (giai đoạn local-first)

Laptop/Mac chạy toàn bộ hệ thống đặt tại quán; mọi thiết bị (điện thoại khách, điện thoại
nhân viên, tablet quầy) **cùng Wi-Fi của quán**.

```bash
./scripts/start-pilot.sh    # tự phát hiện IP LAN, cấu hình QR/CORS đúng, chạy đủ stack
```

Sau khi chạy script: **bắt buộc in lại poster QR** (Bàn & QR → In poster tất cả bàn) để QR
trỏ IP mới. Đổi quán/đổi Wi-Fi → chạy lại script + in lại QR.

Giới hạn chấp nhận ở pilot: OTP đọc tại Mailpit (`http://<IP>:8025`) — người hỗ trợ pilot
đọc mã giúp chủ quán lần đầu; máy phải cắm điện + không sleep (System Settings → ngăn sleep).

## KPI đo mỗi quán (PRD 2.5)

| KPI | Mục tiêu | Cách đo |
|---|---|---|
| Tạo quán → order thử đầu tiên | < 10 phút | Bấm giờ lúc onboarding (dùng Quick Import) |
| Quét QR → thấy menu | < 3 giây trên 4G/Wi-Fi quán | Bấm giờ 5 lần/quán, lấy trung vị |
| Độ trễ báo đơn real-time | < 2 giây | Khách bấm gửi → board/app kêu "ting" |
| Order trùng do retry | 0% | Đối chiếu số order DB vs thực tế cuối ngày |
| Order mất sau khi xác nhận | 0% | Đối chiếu như trên |
| Khách hoàn thành order | > 60% phiên có thêm món vào giỏ | Hỏi quán + xem orders/phiên |

## Quy trình mỗi quán (1 buổi setup + 3–7 ngày chạy)

1. **Setup (30–60 phút):** chạy script → chủ quán tự điền file Excel mẫu (menu + bàn + giờ
   mở cửa + ảnh món trong ZIP) → import → in poster QR → tạo nhân viên → cài Expo Go cho
   điện thoại nhân viên → order thử 1 vòng đầy đủ (quét → gọi món → ting → xác nhận →
   hoàn thành → thu tiền → đóng bàn).
2. **Chạy thật:** quán vận hành bình thường; ghi nhật ký sự cố (mẫu bên dưới).
3. **Kết thúc:** phỏng vấn 15 phút chủ quán + 1 nhân viên + quan sát 2 khách; xuất CSV
   báo cáo; tổng hợp feedback.

## Phân loại feedback

- **P0** — chặn vận hành (mất đơn, không đăng nhập được, sập app): sửa ngay trong pilot.
- **P1** — gây khó chịu rõ rệt nhưng có cách né: sửa trước commercial.
- **P2** — góp ý cải thiện: vào backlog.

## Nhật ký pilot (điền mỗi quán)

| Trường | Ghi chú |
|---|---|
| Quán / ngày | |
| Thời gian onboarding (phút) | mục tiêu <10 cho phần tạo dữ liệu |
| Số order thật / số phiên bàn | |
| Sự cố (mô tả + P0/P1/P2) | |
| Câu nói đáng nhớ của chủ quán/nhân viên/khách | |
| Quán có muốn dùng tiếp không? Trả phí bao nhiêu/tháng? | câu hỏi quan trọng nhất |

## Điều kiện dừng pilot sớm

Gặp P0 không sửa được trong ngày → dừng quán đó, quay lại chế độ giấy, sửa xong mới tiếp.
