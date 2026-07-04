#!/bin/bash
# Backup PostgreSQL TingGo (runbook: docs/runbook-backup-restore.md)
set -e
cd "$(dirname "$0")/.."
mkdir -p backups
FILE="backups/tinggo-$(date +%Y%m%d-%H%M%S).dump"
docker exec tinggo-postgres pg_dump -U tinggo -Fc tinggo > "$FILE"
echo "✅ Backup: $FILE ($(du -h "$FILE" | cut -f1))"
# Giữ 14 bản gần nhất
ls -t backups/tinggo-*.dump 2>/dev/null | tail -n +15 | xargs rm -f 2>/dev/null || true
