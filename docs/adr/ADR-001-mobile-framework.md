# ADR-001: Mobile Framework — React Native

**Status:** Accepted
**Date:** 2026-07-03
**Deciders:** Mobile Developer, Product Owner

## Context

TingGo Merchant cần app mobile (Android + iOS) nhận order real-time với các yêu cầu đặc thù từ PRD: SignalR client ổn định, push notification (foreground/background), âm báo không lặp, TTS, secure storage cho token, khôi phục kết nối khi quay lại foreground (MOB-06). Đội chỉ có 1 Full-stack Mobile Developer, timeline MVP 20 tuần. PRD (mục 13) yêu cầu chọn giữa React Native và Ionic/Capacitor trước Sprint 1 (PRE-02).

## Decision

Dùng **React Native** (khuyến nghị kèm Expo dev client để tăng tốc setup, nhưng cần native module nên không dùng Expo Go thuần).

## Options Considered

### Option A: React Native ✅

| Dimension | Assessment |
|-----------|------------|
| Complexity | Medium |
| Cost | $0 (OSS) |
| Real-time/Push/TTS | Native module ecosystem mạnh: `@microsoft/signalr` chạy tốt, Notifee/FCM cho push, react-native-tts |
| Team familiarity | Phổ biến nhất, dễ tuyển/hỗ trợ |

**Pros:** hiệu năng gần native; background handling (push, âm báo, reconnect) đáng tin cậy hơn WebView; hệ sinh thái thư viện lớn; dùng chung TypeScript với Next.js web.
**Cons:** cần build native (Xcode/Android Studio); upgrade RN đôi khi tốn công.

### Option B: Ionic/Capacitor

| Dimension | Assessment |
|-----------|------------|
| Complexity | Low (nếu đã quen web) |
| Cost | $0 (OSS) |
| Real-time/Push/TTS | Chạy trong WebView — push background và âm thanh tùy biến kém tin cậy hơn, TTS qua plugin hạn chế |
| Team familiarity | Dễ với web dev |

**Pros:** tái dùng tối đa code web; học nhanh.
**Cons:** WebView là rủi ro lớn cho app "báo đơn tức thì" — độ trễ UI, background limitation trên iOS, âm báo/badge kém ổn định.

## Trade-off Analysis

Tính năng lõi của TingGo Merchant là **nhận thông báo real-time đáng tin cậy** — đây chính là điểm yếu nhất của WebView. Chi phí học RN cao hơn một chút được bù bằng độ tin cậy push/âm báo/TTS và khả năng chia sẻ TypeScript types với backend contract.

## Consequences

- Dễ hơn: push notification, âm báo, TTS, SignalR background handling.
- Khó hơn: cần môi trường build native; mobile dev phải nắm một phần native (permissions, notification channels).
- Cần revisit: nếu sau này làm TingGo Kitchen (tablet display) có thể cân nhắc reuse RN hoặc web.

## Action Items

1. [ ] Init RN project (TypeScript) + navigation + theme (Sprint 1)
2. [ ] PoC SignalR + push + TTS trên thiết bị thật (Sprint 0)
3. [ ] Chốt secure storage (Keychain/Keystore) cho token
