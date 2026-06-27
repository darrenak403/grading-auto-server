# Chạy Backend (`be/`)

Tổng quan: [`README.md`](README.md). FE **repo riêng**.

## Setup lần đầu

```bash
cp docker/.env.example docker/.env.local   # điền giá trị (xem bên dưới)
task up                                     # khởi động infra
```

Mở 2 terminal:

```bash
task api      # terminal 1 — API
task worker   # terminal 2 — Worker (grading background)
```

Swagger: http://localhost:5049/swagger  
pgWeb: http://localhost:8081  
RabbitMQ: http://localhost:15672

---

## Task reference

| Lệnh | Mô tả |
|------|--------|
| `task up` | Khởi động infra (Postgres, SQL Server, RabbitMQ, pgWeb) |
| `task down` | Tắt infra |
| `task logs` | Xem log infra |
| `task api` | Chạy API — `dotnet run` |
| `task worker` | Chạy Worker — `dotnet run` |

---

## Cấu hình `docker/.env.local`

Copy từ `docker/.env.example` rồi điền:

| Biến | Ý nghĩa | Ví dụ | Ghi chú |
|------|---------|-------|--------|
| `POSTGRES_PASSWORD` | Mật khẩu Postgres | `postgres` | |
| `POSTGRES_PUBLISH_PORT` | Port Postgres ra host | `5439` | Phải khớp với connection string |
| `MSSQL_SA_PASSWORD` | Mật khẩu SQL Server | `YourStr0ng!Pass` | |
| `RABBITMQ_USER` | RabbitMQ username | `guest` | |
| `RABBITMQ_PASS` | RabbitMQ password | `guest` | |
| `WORKER_MAX_CONCURRENT_JOBS` | Số job chấm song song (PE) | `3` (mặc định: auto từ CPU) | Nếu không đặt → `Math.Clamp(CoreCount - 1, 1, 8)` |
| `WORKER_SUBMISSION_TIMEOUT_SECONDS` | Timeout per bài (giây) | `90` | Vượt quá → kill process, mark Failed |

> `POSTGRES_PUBLISH_PORT` phải khớp với `appsettings.Development.json` (connection string).

---

## Database migration

Migration chạy tự động khi khởi động API và Worker (`MigrateAsync()` trong `Program.cs`).

Tạo migration mới:

```bash
cd GradingSystem.Infrastructure
dotnet ef migrations add <TênMigration> --startup-project ../GradingSystem.Api
```

---

## Luồng chấm lab (tóm tắt)

```
1. Tạo LabAssignment
2. Import test case JSON  →  POST /api/v1/lab-assignments/{id}/testcases/batch
3. Approve test case      →  PATCH /api/v1/lab-testcases/{id}/approve
4. Upload bài sinh viên  →  POST /api/v1/lab-submissions?assignmentId={id}
                             (multipart, file đặt tên: SE180234_NguyenVanA.zip)
5. Trigger chấm          →  POST /api/v1/lab-assignments/{id}/grade
6. Xem kết quả           →  GET  /api/v1/lab-submissions/{id}/results
```

Tài liệu API đầy đủ: [`docs/fe/2026-05-27-feat-lab-grading.md`](docs/fe/2026-05-27-feat-lab-grading.md)  
Hướng dẫn tạo test case: [`docs/testcase-prompt-template.md`](docs/testcase-prompt-template.md)
