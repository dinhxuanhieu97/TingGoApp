# TingGo

Nền tảng menu QR và nhận order real-time cho quán ăn, quán nước và cafe.
**Quét bàn, gọi món, quán nhận ngay.**

## Tài liệu

- `PRD.md` — Product requirement + roadmap 20 tuần
- `DEVELOPMENT_PLAN.md` — Kế hoạch thực thi với Claude Cowork
- `docs/adr/` — 5 quyết định kiến trúc đã chốt
- `CLAUDE.md` — Convention bắt buộc khi code

## Cấu trúc

```
backend/            # ASP.NET Core (.NET 10) modular monolith
apps/customer-web/  # Next.js — khách quét QR
apps/merchant-web/  # Next.js — quản lý quán
apps/merchant-mobile/ # React Native — app chủ quán/nhân viên
docker-compose.yml  # PostgreSQL 17, Redis 7, Mailpit (SMTP dev)
```

## Chạy dev

```bash
docker compose up -d
cd backend
dotnet build TingGo.slnx
dotnet run --project src/TingGo.Api    # http://localhost:5080
```

Kiểm tra: `GET /health` (postgres + redis), `GET /api/v1/ping`, Mailpit UI: http://localhost:8025

## Test

```bash
cd backend && dotnet test TingGo.slnx
```
