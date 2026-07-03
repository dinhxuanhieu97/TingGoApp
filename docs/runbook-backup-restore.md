# Runbook — Backup & Restore PostgreSQL (NFR 5.2)

## Dev (Docker local)

```bash
# Backup
docker exec tinggo-postgres pg_dump -U tinggo -Fc tinggo > backup-$(date +%Y%m%d-%H%M).dump

# Restore (DỪNG API trước)
docker exec -i tinggo-postgres pg_restore -U tinggo -d tinggo --clean --if-exists < backup-XXXX.dump

# Verify sau restore
docker exec tinggo-postgres psql -U tinggo -d tinggo -c "SELECT count(*) FROM orders"
curl http://localhost:5080/health   # phải Healthy
```

## Staging/Production AWS (ADR-002 — từ Sprint 9)

- **RDS PostgreSQL**: bật automated backups, retention ≥ 7 ngày, PITR (point-in-time recovery).
- **Restore drill mỗi tháng**: restore snapshot vào instance tạm → chạy integration tests trỏ vào đó → hủy instance.
- **Trước mỗi migration production**: tạo manual snapshot, ghi tag `pre-migration-{version}`.

## Quy tắc

1. `orders`, `payments` không xóa vật lý — restore không bao giờ được làm mất lịch sử giao dịch.
2. Sau restore: chạy `dotnet ef database update` để chắc schema khớp phiên bản code đang deploy.
3. Sự cố mất dữ liệu → làm theo `engineering:incident-response`, ghi timeline ngay từ phút đầu.
