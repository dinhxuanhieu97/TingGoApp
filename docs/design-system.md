# TingGo Design System (v1 — 2026-07-04)

Nguyên tắc: **mobile-first cho khách** (100% dùng điện thoại), **tablet-first cho quán**
(order board đặt ở quầy), tham chiếu pattern các app F&B thịnh hành (GrabFood/ShopeeFood):
CTA nổi dính đáy, bottom-sheet chọn món, chip danh mục cuộn ngang, thẻ bo góc lớn.

## Design Tokens (khai báo tại `globals.css` mỗi app, dùng qua class Tailwind `brand-*`)

### Màu
| Token | Giá trị | Dùng cho |
|---|---|---|
| `brand-50` | #fff7ed | Nền app |
| `brand-100` | #ffedd5 | Nền chip/badge nhạt |
| `brand-600` | #ea580c | **Primary** — nút chính, logo, giá tiền |
| `brand-700` | #c2410c | Hover primary |
| `brand-800` | #9a3412 | Text trên nền brand nhạt |
| `success` / `success-bg` | #16a34a / #dcfce7 | Còn bán, đã thanh toán, real-time on |
| `warning` / `warning-bg` | #d97706 / #fef3c7 | Yêu cầu chờ, cảnh báo import |
| `danger` / `danger-bg` | #dc2626 / #fee2e2 | Từ chối, hết hàng, lỗi |

Quy tắc: **không hardcode hex trong component** — luôn dùng token.

### Chữ (font Geist, hệ Tailwind mặc định)
- Tiêu đề trang: `text-lg font-bold` (mobile) / `text-xl` (≥md)
- Tên món/order: `text-sm font-medium`; Giá: `text-sm font-semibold text-brand-600`
- Phụ chú: `text-xs text-gray-500`; tối thiểu 12px — không nhỏ hơn

### Khoảng cách & bo góc
- Card: `rounded-2xl` (16px), padding `p-3`/`p-4`; Sheet: `rounded-t-3xl`
- Touch target tối thiểu **44×44px** (nút hành động mobile: `py-3`)
- Khe giữa card: `gap-2` (mobile) / `gap-3` (≥md)

### Breakpoints sử dụng
- `<640` mobile (khách; nhân viên cầm tay) — 1 cột, CTA dính đáy, safe-area
- `640–1024` tablet (quầy) — board cuộn ngang snap 2 cột nhìn thấy, grid 2 cột
- `>1024` desktop — board 4 cột, grid 3-4 cột

## Components chuẩn hóa
- **MerchantNav** (`components/MerchantNav.tsx`): header dùng chung 5 trang — logo, nav
  cuộn ngang trên mobile, active state gạch chân brand, tên quán ẩn trên mobile nhỏ.
- **Nút primary**: `rounded-xl bg-brand-600 text-white font-semibold hover:bg-brand-700 disabled:opacity-50`
- **Badge trạng thái**: `rounded-full px-2 py-0.5 text-xs font-semibold` + cặp màu semantic
- **Sheet (customer)**: đáy, `rounded-t-3xl`, thanh kéo `h-1 w-10 bg-gray-300`, `pb-safe`

## UX copy (design:ux-copy pass)
- CTA hành động cụ thể: "Gửi order" không phải "OK"; kèm giá khi xác nhận ("Thêm · 29.000₫")
- Empty state = hướng dẫn bước tiếp theo, không chỉ "trống"
- Lỗi: nói cách sửa ("Món vừa hết hàng. Vui lòng chọn món khác.")
- Tiếng Việt thân thiện, không thuật ngữ kỹ thuật với khách/chủ quán
