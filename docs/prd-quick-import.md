# PRD — TingGo Quick Import (bản rút gọn triển khai)

Bản đầy đủ do Product Owner cung cấp 2026-07-04. File này tóm tắt phạm vi đã chốt cho MVP
và ghi chú các điểm thích nghi với kiến trúc local-first hiện tại.

## Phạm vi MVP (theo mục 17 PRD gốc)

- Excel theo template v2 (code-based: area_code, product_code, group_code...)
- Staging: import_jobs / import_rows / import_issues — KHÔNG ghi DB chính khi parse
- Preview: summary + issues (ERROR chặn commit, WARNING cho phép, INFO tham khảo)
- Create Only; trùng code trong file hoặc với DB → ERROR
- Commit trong transaction; QR tự tạo cho bàn active; menu tạo ở trạng thái NHÁP
- File báo lỗi tải về (xlsx); hủy job; audit log; chặn double-commit

## Thích nghi so với PRD gốc (quyết định kỹ thuật, đổi được sau)

1. **Parse đồng bộ trong request upload** thay vì object storage + worker — hợp local-first
   (ADR-002), giới hạn 10MB/5.000 dòng nên đủ nhanh. Khi lên AWS: tách sang presigned URL + worker.
2. **Sheet OpeningHours, Translations: chưa xử lý** — DB chưa có bảng giờ mở cửa/bản dịch.
   Parser bỏ qua kèm INFO. Bổ sung khi làm i18n đầy đủ.
3. **Venue sheet**: chỉ áp dụng wifi_name, default_locale, currency_code, timezone
   (các cột DB đang có). phone/email/address/tax_rate → INFO "chưa hỗ trợ".
4. **capacity của bàn**: DB chưa có cột → INFO, không chặn.
5. **ZIP + hình ảnh**: Sprint Import 3 — chưa làm ở lượt này (cột image_file được parse
   và cảnh báo WARNING nếu điền, vì chưa xử lý ảnh từ file).
6. product_code lưu vào cột `sku` của products; category/area đối chiếu trùng theo tên với DB,
   theo code trong nội bộ file.

## Trạng thái job (rút gọn)

VALIDATING → NEEDS_REVIEW (có ERROR) | READY_TO_IMPORT → IMPORTING → COMPLETED |
COMPLETED_WITH_WARNINGS | FAILED | CANCELLED

## API

GET  /venues/{venueId}/imports/template          (template v2 kèm README + ví dụ)
POST /venues/{venueId}/imports                   (multipart file → parse + validate → staging)
GET  /imports/{importId}                          (status + summary + canCommit)
GET  /imports/{importId}/issues
POST /imports/{importId}/commit
POST /imports/{importId}/cancel
GET  /imports/{importId}/error-file

Acceptance criteria: theo mục 16 PRD gốc (trừ ZIP/ảnh — AC 8 hoãn cùng Sprint Import 3).
