# TingGo Merchant Mobile (Expo / React Native — ADR-001)

App nhận order real-time cho chủ quán và nhân viên (PRD MOB-01..06).

## Đã có

- Đăng nhập: chủ quán (OTP email) + nhân viên (Venue ID + mã NV + PIN), token trong SecureStore, auto refresh
- Tab **Order**: nhận đơn real-time qua SignalR, chống lặp `eventId`, xử lý trạng thái (xác nhận/từ chối/làm/sẵn sàng/hoàn thành), resync khi reconnect hoặc app quay lại foreground (MOB-06)
- Tab **Món**: tìm kiếm + bật/tắt món tức thì với optimistic update (MOB-04)
- **TTS** (MOB-05): đọc "Bàn X có đơn mới: 2 cà phê sữa..." qua `ITtsEngine` — mặc định on-device (expo-speech), bật/tắt trên header. CapCut/FPT.AI cắm sau qua interface (ADR-005)

## Chạy dev

```bash
# Backend + docker compose phải đang chạy
cd apps/merchant-mobile
npm install --include=dev

# Máy thật/emulator không thấy localhost của máy dev — trỏ IP LAN:
EXPO_PUBLIC_API_URL=http://192.168.x.x:5080/api/v1 npx expo start
# Quét QR bằng Expo Go (iOS/Android)
```

## Chưa làm (sprint sau)

- Push notification background (FCM + `IPushSender` backend đã sẵn) — cần dev client, không chạy trong Expo Go
- Âm báo tùy chỉnh, badge số đơn
- Build release Android/iOS (EAS)
