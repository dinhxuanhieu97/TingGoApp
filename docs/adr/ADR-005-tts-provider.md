# ADR-005: TTS — on-device mặc định, provider chính thức cho premium (KHÔNG dùng CapCut private API)

**Status:** Accepted (2026-07-04 — sau khi đánh giá tài liệu capcut-tts-api do PO cung cấp)
**Date:** 2026-07-03, cập nhật 2026-07-04
**Deciders:** Mobile Developer, Product Owner

## Kết quả đánh giá CapCut (2026-07-04)

Tài liệu PO cung cấp (`capcut-tts-api/` — giữ local, không track git) là client
**reverse-engineer API nội bộ CapCut**: tái tạo chữ ký request từ luồng bị bắt gói,
giả lập device profile/session của app CapCut desktop. 129 voice, có tiếng Việt.

**Quyết định: KHÔNG dùng cho production.** Lý do:
1. Không phải API thương mại chính thức → gần như chắc chắn vi phạm ToS CapCut/ByteDance.
2. Rủi ro vận hành: CapCut đổi cơ chế ký → TTS chết đột ngột giữa lúc quán chạy.
3. Rủi ro pháp lý cho sản phẩm bán cho khách hàng.

**Phương án premium thay thế** (tích hợp qua `ITtsEngine` khi cần, không đổi kiến trúc):
FPT.AI TTS (chuyên tiếng Việt), Google Cloud TTS, Azure Speech — API chính thức, SLA rõ.
Kích hoạt khi PO cung cấp API key của provider được chọn.

## Context

MOB-05: app đọc to đơn mới ("Bàn số 5 có đơn mới: hai cà phê sữa..."). TTS thuộc gói Premium nhưng PRD yêu cầu kiến trúc chuẩn bị từ đầu. Yêu cầu: giọng tiếng Việt tự nhiên, độ trễ thấp (đọc ngay khi đơn đến), hoạt động khi app foreground, chống đọc trùng sự kiện. Product Owner đề xuất dùng API của CapCut và sẽ cung cấp chi tiết (endpoint, pricing, giới hạn) sau.

## Decision

1. Xây **interface `ITtsEngine`** trong mobile app ngay từ đầu — mọi nơi cần đọc đơn chỉ gọi interface, không gọi provider trực tiếp.
2. **Implementation mặc định (miễn phí, offline): TTS hệ điều hành** (`react-native-tts` — AVSpeechSynthesizer/Android TTS, có giọng vi-VN) — dùng cho dev và fallback.
3. **Implementation CapCut API** thêm sau khi có thông tin — quyết định chính thức sẽ cập nhật ADR này.

## Options Considered

### Option A: CapCut API (đề xuất của PO — chờ xác minh)
**Pros:** giọng Việt tự nhiên, chất lượng cao.
**Cons:** cần xác minh: có API public/thương mại chính thức không, pricing, rate limit, ToS cho phép dùng trong app thương mại, độ trễ sinh audio (server-side generation có thể chậm hơn TTS on-device); phụ thuộc mạng.

### Option B: TTS on-device (fallback mặc định) ✅ cho dev
**Pros:** miễn phí, offline, độ trễ ~0, không phụ thuộc bên thứ ba.
**Cons:** giọng vi-VN máy móc hơn, khác nhau giữa thiết bị.

### Option C: Cloud TTS khác (Google/Azure/FPT.AI)
**Pros:** API chính thức, SLA rõ, giọng Việt tốt (FPT.AI chuyên tiếng Việt).
**Cons:** chi phí theo ký tự; vẫn phụ thuộc mạng. Giữ làm phương án so sánh khi đánh giá CapCut.

## Trade-off Analysis

Vì thông tin CapCut chưa đủ, quyết định an toàn nhất là **tách interface + ship fallback on-device**. Điều này cho phép demo TTS ngay ở Sprint 8 với chi phí 0, và cắm CapCut (hoặc FPT.AI nếu CapCut không khả thi về ToS/pricing) mà không đổi kiến trúc. Audio đã sinh nên cache theo `order_id` để không tốn phí đọc lại.

## Consequences

- Dễ hơn: dev/test TTS không cần chờ thông tin CapCut; đổi provider không ảnh hưởng logic chống đọc trùng.
- Khó hơn: duy trì 2 implementation khi CapCut vào.
- Revisit: khi PO cung cấp chi tiết CapCut API → đánh giá pricing/ToS/latency, cập nhật status ADR thành Accepted.

## Action Items

1. [ ] Interface `ITtsEngine` + implementation on-device (Sprint 0 PoC, hoàn thiện Sprint 8)
2. [ ] Logic chống đọc trùng theo `eventId` (dùng chung với âm báo)
3. [ ] PO cung cấp tài liệu CapCut API → đánh giá và cập nhật ADR
4. [ ] Benchmark độ trễ CapCut vs on-device vs FPT.AI trước khi chốt Premium
