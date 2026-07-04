#!/bin/bash
# TingGo — chạy chế độ PILOT tại quán: mọi thiết bị cùng Wi-Fi truy cập qua IP LAN của máy này.
set -e
cd "$(dirname "$0")/.."

IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null)
if [ -z "$IP" ]; then echo "❌ Không lấy được IP LAN — kiểm tra Wi-Fi"; exit 1; fi
echo "🏪 TingGo Pilot — IP LAN: $IP"

echo "→ Hạ tầng (postgres, redis, mailpit)..."
docker compose up -d

echo "→ Backend API (0.0.0.0:5080, QR trỏ http://$IP:3001)..."
pkill -f "TingGo.Api" 2>/dev/null || true
PublicWeb__BaseUrl="http://$IP:3001" \
Cors__Origins__0="http://localhost:3000" \
Cors__Origins__1="http://localhost:3001" \
Cors__Origins__2="http://$IP:3000" \
Cors__Origins__3="http://$IP:3001" \
nohup dotnet run --project backend/src/TingGo.Api > /tmp/tinggo-api.log 2>&1 &

echo "→ Merchant Web (http://$IP:3000)..."
pkill -f "next dev" 2>/dev/null || true
(cd apps/merchant-web && NEXT_PUBLIC_API_URL="http://$IP:5080/api/v1" \
  nohup npx next dev -H 0.0.0.0 > /tmp/next-mw.log 2>&1 &)

echo "→ Customer Web (http://$IP:3001)..."
(cd apps/customer-web && NEXT_PUBLIC_API_URL="http://$IP:5080/api/v1" \
  nohup npx next dev -H 0.0.0.0 -p 3001 > /tmp/next-cw.log 2>&1 &)

sleep 10
echo ""
echo "✅ Kiểm tra:"
echo "   API:          $(curl -s -o /dev/null -w '%{http_code}' http://$IP:5080/health) (mong đợi 200)"
echo "   Merchant Web: $(curl -s -o /dev/null -w '%{http_code}' http://$IP:3000) (mong đợi 200)"
echo "   Customer Web: $(curl -s -o /dev/null -w '%{http_code}' http://$IP:3001) (mong đợi 200)"
echo ""
echo "📋 Quán dùng:"
echo "   • Quản lý (laptop/tablet): http://$IP:3000"
echo "   • OTP đăng nhập xem tại:   http://$IP:8025 (Mailpit)"
echo "   • Mobile app: EXPO_PUBLIC_API_URL=http://$IP:5080/api/v1 npx expo start (trong apps/merchant-mobile)"
echo ""
echo "⚠️  QUAN TRỌNG: vào Bàn & QR → 'In poster tất cả bàn' SAU khi chạy script này"
echo "   để QR trỏ đúng http://$IP:3001 (QR in trước đó trỏ localhost sẽ không dùng được)."
echo ""
echo "Dừng: ./scripts/stop-pilot.sh"
