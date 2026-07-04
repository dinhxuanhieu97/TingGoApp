# Checklist tính năng trang quản trị TingGo

Đối chiếu với các sản phẩm thương mại phổ biến tại VN: **KiotViet FnB, Sapo FnB, iPos, GrabFood Merchant, ShopeeFood Partner**. Cập nhật: 2026-07-04.

Ký hiệu: ✅ đã có · 🟡 có một phần · ❌ chưa có · ⭐ đề xuất ưu tiên kế tiếp

## 1. Cơ bản (bắt buộc để vận hành hằng ngày)

| Tính năng | Trạng thái | Ghi chú |
|---|---|---|
| Danh mục: thêm / sửa tên / xóa | ✅ | Xóa chặn khi còn món (phải chuyển/lưu trữ trước) |
| Danh mục: ẩn/hiện với khách | ✅ | `isVisible` |
| Danh mục: sắp xếp thứ tự | ✅ | API sort đã có |
| Món: thêm / sửa (tên, giá, mô tả, ảnh, danh mục) | ✅ | Modal sửa món |
| Món: xóa (lưu trữ) | ✅ | Archive — order cũ giữ snapshot |
| Món: bật/tắt "còn bán / hết hàng" | ✅ | Cả web + mobile |
| Món: size (variants) + topping (modifiers) | ✅ | CRUD variant, modifier group/option |
| Món: sắp xếp thứ tự ↑↓ | ✅ | |
| Món: tìm kiếm (không dấu) | ✅ | |
| Nhân viên: thêm / sửa tên, vai trò / đặt lại PIN / thu hồi–kích hoạt | ✅ | PATCH staff mới thêm |
| Bàn & khu vực: tạo hàng loạt, QR từng bàn, poster QR | ✅ | |
| Order board real-time + chuyển trạng thái | ✅ | FIFO theo thời điểm vào trạng thái |
| Thu tiền (tiền mặt, chuyển khoản QR) + đóng bàn | ✅ | |
| Báo cáo ngày: doanh thu, số đơn, món bán chạy, export CSV | ✅ | |
| Cài đặt quán: tên, Wi-Fi, giờ mở cửa, QR bank, ngôn ngữ/tiền tệ | ✅ | |
| Nhập nhanh Excel/ZIP (menu, bàn, giờ, bản dịch) | ✅ | Quick Import v2 |

## 2. Trung cấp (nên có khi quán chạy ổn định)

| Tính năng | Trạng thái | Ghi chú |
|---|---|---|
| ⭐ Kéo–thả sắp xếp danh mục/món (thay nút ↑↓) | ❌ | dnd-kit; chuẩn KiotViet/Sapo |
| ⭐ Sửa nhanh giá ngay trên danh sách món (inline edit) | ❌ | Đỡ mở modal |
| ⭐ Lịch sử order (đã xong/hủy) + lọc theo ngày/bàn/trạng thái | 🟡 | API `/orders?status=` có; UI chưa có trang riêng |
| ⭐ Âm thanh + badge số đơn mới trên tab Order | 🟡 | Có beep; chưa có badge đếm trên nav |
| Chọn nhiều món → thao tác hàng loạt (tắt bán, đổi danh mục) | ❌ | |
| Nhân bản món (duplicate) | ❌ | Tạo món tương tự nhanh |
| Ảnh món: crop/nén tự động phía client | ❌ | Giảm dung lượng upload |
| Giảm giá / khuyến mãi theo món hoặc theo đơn | ❌ | Cần bảng promotions |
| Ghi chú nội bộ cho bàn/order (quán tự ghi) | ❌ | |
| Phân quyền chi tiết theo vai trò trên web (cashier không vào Cài đặt...) | 🟡 | Backend chặn; UI chưa ẩn menu theo role |
| Nhật ký hoạt động (ai sửa giá, ai hủy đơn) | 🟡 | Có order history; chưa có audit log chung + UI |
| Báo cáo theo khoảng ngày tùy chọn + so sánh kỳ trước | 🟡 | Đang cố định 7/30 ngày |
| Multi-venue: chuyển quán trong 1 tài khoản | 🟡 | Data model có; UI mới chọn venue đầu tiên |

## 3. Nâng cao (mở rộng thương mại)

| Tính năng | Trạng thái | Ghi chú |
|---|---|---|
| Bếp riêng (KDS): màn hình bếp chỉ hiện món cần làm, gộp theo món | ❌ | Chuẩn iPos/KiotViet |
| Tách/gộp đơn, chuyển bàn | ❌ | |
| In hóa đơn (máy in nhiệt) / in phiếu bếp | ❌ | ESC/POS qua LAN |
| payOS / VNPay-QR động (đối soát tự động) | ❌ | ADR-004 để sau, đã chốt |
| Tồn kho nguyên liệu + trừ định mức | ❌ | Phân hệ lớn |
| Khách hàng thân thiết / tích điểm | ❌ | |
| Đặt món mang về (không cần QR bàn) | ❌ | Link menu công khai đã có nền |
| Thống kê giờ cao điểm, heatmap theo khung giờ | 🟡 | API hourly có; UI chưa vẽ biểu đồ |
| Đa chi nhánh: báo cáo hợp nhất organization | ❌ | |
| AWS deploy + backup tự động cloud | ❌ | Chờ theo kế hoạch (ADR-002) |

## Đề xuất lộ trình kế tiếp (theo giá trị/công sức)

1. **Lịch sử order + lọc** — quán cần tra soát khiếu nại hằng ngày.
2. **Badge đơn mới + âm thanh bật/tắt** — giám sát tốt hơn khi mở tab khác.
3. **Kéo–thả sắp xếp + inline edit giá** — thao tác menu nhanh gấp nhiều lần.
4. **Ẩn menu theo vai trò + audit log** — chuẩn thương mại khi có nhiều nhân viên.
5. **KDS màn hình bếp** — bước nhảy giá trị lớn nhất cho quán đông.
