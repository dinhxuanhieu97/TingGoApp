# TingGo Customer Web (Next.js + TypeScript)

Web khách quét QR: menu, giỏ hàng, gửi order, theo dõi trạng thái (PRD CUS-01..09).

Init (Sprint 1, chạy trên máy dev — cần mạng):
```bash
npx create-next-app@latest . --typescript --app --tailwind --eslint --src-dir
npm i @microsoft/signalr
```
API base: http://localhost:5080/api/v1 — đặt trong `.env.local` (`NEXT_PUBLIC_API_URL`).
