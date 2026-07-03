# ADR-002: Cloud Provider — AWS, phát triển local trước

**Status:** Accepted
**Date:** 2026-07-03
**Deciders:** Web Developer, Product Owner

## Context

MVP cần môi trường dev, staging và production-like cho pilot (Sprint 10). Stack: ASP.NET Core, PostgreSQL, Redis, SignalR, CDN cho ảnh. Yêu cầu phi chức năng: uptime 99,9% (giai đoạn thương mại), backup tự động, menu load < 3s trên 4G tại Việt Nam. Ngân sách giai đoạn MVP thấp.

## Decision

**Giai đoạn 1 (Sprint 1–8): 100% local** — Docker Compose (PostgreSQL, Redis, API, web). Không tốn chi phí cloud, không phụ thuộc mạng.
**Giai đoạn 2 (Sprint 9–10): AWS** cho staging/pilot — region `ap-southeast-1` (Singapore) để latency VN thấp.

Thành phần AWS đề xuất tối giản: ECS Fargate hoặc 1 EC2 chạy Docker Compose (pilot chưa cần orchestration), RDS PostgreSQL (backup tự động), ElastiCache Redis (hoặc Redis cùng máy khi pilot), S3 + CloudFront cho ảnh menu.

## Options Considered

### Option A: AWS ✅
**Pros:** đủ mọi managed service, region Singapore gần VN, RDS backup/restore đáp ứng NFR 5.2, tài liệu nhiều.
**Cons:** chi phí cao hơn VPS nội địa; cần quản lý IAM/billing cẩn thận.

### Option B: Azure
**Pros:** hợp hệ .NET, Azure SignalR Service quản lý sẵn.
**Cons:** đội chưa quen; SignalR self-host + Redis backplane đủ dùng ở quy mô pilot nên lợi thế không lớn.

### Option C: VPS Việt Nam (Viettel/FPT)
**Pros:** rẻ, latency nội địa tốt nhất.
**Cons:** thiếu managed DB/backup tự động — vi phạm NFR 5.2; khó scale khi thương mại hóa.

## Trade-off Analysis

Dev local trước loại bỏ hoàn toàn chi phí và độ phức tạp cloud trong 16 tuần đầu — đúng tinh thần MVP. AWS Singapore cân bằng giữa managed services (backup, restore, CDN) và latency chấp nhận được (~30–40ms từ VN). Chi phí pilot ước tính thấp nếu dùng 1 Fargate task/EC2 nhỏ + RDS t4g.micro.

## Consequences

- Dễ hơn: onboarding dev (chỉ cần Docker), backup/restore theo NFR, CDN ảnh.
- Khó hơn: cần script IaC (Terraform hoặc CDK) trước Sprint 9; hai môi trường (local/AWS) phải đồng nhất qua Docker image.
- Revisit: khi thương mại hóa xem xét multi-AZ, autoscaling, hoặc region/edge gần VN hơn.

## Action Items

1. [ ] Docker Compose hoàn chỉnh trong Sprint 1
2. [ ] Viết IaC skeleton (Terraform/CDK) trước Sprint 9
3. [ ] Dựng staging AWS + RDS backup + CloudFront trong Sprint 9
