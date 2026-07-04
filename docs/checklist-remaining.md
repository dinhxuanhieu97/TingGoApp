# Checklist các mục CHƯA làm (cập nhật 2026-07-04)

## ✅ Đã đóng toàn bộ

- PRD MVP mục 3 (CUS-01..09, MER-01..08, MOB-01..06 phần làm được không cần thiết bị/hạ tầng)
- TingGo Quick Import trọn bộ MVP (Excel + ZIP ảnh + staging/preview/commit)
- Real-time SignalR, thanh toán cash/QR tĩnh, báo cáo, rate limiting, CI, load test
- 5 ADR đã Accepted (TTS chốt on-device 2026-07-04)

## 🔶 Việc thực địa (không phải code) — NGAY BÂY GIỜ

- [ ] **Pilot 5 quán** — kit đã sẵn: `scripts/start-pilot.sh` + `docs/pilot-plan.md`
      + `docs/pilot-onboarding-guide.md`. Người thực hiện: Product Owner.
- [ ] Quyết định giá bán/tháng sau pilot (dữ liệu từ câu hỏi cuối nhật ký pilot)

## 🔷 Sau pilot (code — theo feedback + backlog thương mại)

- [ ] Sửa P0/P1 từ pilot
- [ ] AWS staging + production (Terraform skeleton sẵn tại `infra/terraform` — cần
      credentials + duyệt ~$40-60/tháng); email OTP thật qua SES thay Mailpit
- [ ] Mobile build release (EAS) + push FCM native (Expo push đã chạy cho dev)
- [ ] payOS (ADR-004 Commercial V1) — schema payments đã sẵn
- [ ] Monitoring/APM + alerting (Sprint 10 phần production)
- [ ] Security review chuyên sâu + restore drill theo runbook

## 🔹 Backlog thương mại (đã chốt hoãn, chưa có ngày)

- [ ] i18n bản dịch menu (bảng product_translations) + sheet Translations trong import
- [ ] TTS premium qua provider chính thức (FPT.AI/Google/Azure) — chỉ cần API key, cắm qua ITtsEngine
- [ ] Import nâng cao: OCR/AI chụp menu, Google Sheets, upsert mode, CSV
- [ ] Đa chi nhánh, nhân bản menu giữa chi nhánh
- [ ] Máy in bếp, loyalty, voucher, đặt bàn (ngoài phạm vi MVP theo PRD mục 4)

## Ghi chú kỹ thuật tồn đọng (nhỏ, không chặn)

- Xóa description món về rỗng chưa được (PATCH giữ giá trị cũ khi null)
- Sắp xếp thứ tự món bằng kéo-thả chưa có UI (backend có sort_order)
- Mobile: staff login nhập Venue ID dài — nên đổi mã quán ngắn khi lên production
