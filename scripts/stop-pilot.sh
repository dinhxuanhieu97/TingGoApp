#!/bin/bash
pkill -f "TingGo.Api" 2>/dev/null || true
pkill -f "next dev" 2>/dev/null || true
cd "$(dirname "$0")/.." && docker compose stop
echo "✅ Đã dừng toàn bộ TingGo."
