# TingGo Merchant Mobile (React Native — ADR-001)

App nhận order real-time: push, âm báo, TTS, xử lý order (PRD MOB-01..06).

Init (Sprint 1):
```bash
npx @react-native-community/cli init TingGoMerchant --directory . --pm npm
npm i @microsoft/signalr react-native-tts @notifee/react-native @react-native-firebase/app @react-native-firebase/messaging react-native-keychain
```
Lưu ý: TTS đi qua interface `ITtsEngine` (xem docs/adr/ADR-005) — không gọi provider trực tiếp.
