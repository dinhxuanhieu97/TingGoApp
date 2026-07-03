# TingGo staging — skeleton IaC (ADR-002: AWS ap-southeast-1, dựng ở Sprint 9-10)
# CHƯA APPLY — cần AWS credentials + duyệt chi phí từ Product Owner.
# Ước tính pilot: ~$40-60/tháng (t4g.small EC2 + db.t4g.micro RDS + S3/CloudFront).

terraform {
  required_version = ">= 1.6"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 5.0" }
  }
}

provider "aws" {
  region = "ap-southeast-1" # Singapore — latency VN ~30-40ms
}

variable "db_password" {
  type      = string
  sensitive = true
}

# --- RDS PostgreSQL: backup tự động theo NFR 5.2 ---
resource "aws_db_instance" "tinggo" {
  identifier              = "tinggo-staging"
  engine                  = "postgres"
  engine_version          = "17"
  instance_class          = "db.t4g.micro"
  allocated_storage       = 20
  db_name                 = "tinggo"
  username                = "tinggo"
  password                = var.db_password
  backup_retention_period = 7
  skip_final_snapshot     = false
  final_snapshot_identifier = "tinggo-staging-final"
}

# --- EC2 chạy Docker Compose (API + Redis) — đủ cho pilot 5 quán ---
resource "aws_instance" "app" {
  ami           = "resolve-latest-al2023-arm64" # TODO: data source aws_ami
  instance_type = "t4g.small"
  tags          = { Name = "tinggo-staging-app" }
}

# --- S3 + CloudFront cho ảnh menu (NFR 5.1) ---
resource "aws_s3_bucket" "images" {
  bucket = "tinggo-staging-images"
}

# TODO Sprint 10: CloudFront distribution, security groups, ALB + ACM cert,
# secrets qua SSM Parameter Store (Jwt__Secret, ConnectionStrings__Postgres).
