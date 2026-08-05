# Chạy Backend (`be/`)

Tổng quan: [`README.md`](README.md). FE **repo riêng**.

## Setup lần đầu

```bash
cp docker/.env.example docker/.env.local   # điền giá trị (xem bên dưới)
# Trong docker/.env.local, bỏ comment:
# Playwright__BrowserCdpEndpoint=http://127.0.0.1:9222
task up                                     # khởi động infra
task run                                    # Playwright + API + Worker
```

`task run` build solution một lần, sau đó chạy API và Worker song song. Chromium chạy nền; nhấn `Ctrl+C` để dừng API/Worker và dùng `task playwright:down` khi không cần Chromium nữa.

Nếu cần debug từng ứng dụng ở terminal riêng:

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
| `task run` | Khởi động Playwright, build solution, chạy API và Worker song song |
| `task down` | Tắt infra |
| `task logs` | Xem log infra |
| `task api` | Chạy API — `dotnet run` |
| `task worker` | Chạy Worker — `dotnet run` |
| `task playwright:up` | Khởi động Chromium CDP |
| `task playwright:down` | Tắt Chromium CDP |

---

## Cấu hình `docker/.env.local`

Copy từ `docker/.env.example` rồi điền:

| Biến | Ý nghĩa | Ví dụ | Ghi chú |
|------|---------|-------|--------|
| `POSTGRES_PASSWORD` | Mật khẩu Postgres | `postgres` | |
| `POSTGRES_PUBLISH_PORT` | Port Postgres ra host | `5439` | Phải khớp với connection string |
| `SA_PASSWORD` | Mật khẩu SQL Server | `YourStr0ng!Pass` | |
| `RABBITMQ_USER` | RabbitMQ username | `guest` | |
| `RABBITMQ_PASSWORD` | RabbitMQ password | `guest` | |
| `PLAYWRIGHT_CDP_PORT` | Port Chromium CDP | `9222` | Dùng khi chạy `task run` |
| `Playwright__BrowserCdpEndpoint` | Endpoint Chromium cho Worker | `http://127.0.0.1:9222` | Bỏ comment để chấm câu Razor qua Playwright |
| `WORKER_MAX_CONCURRENT_JOBS` | Số job chấm song song (PE) | `1` (mặc định: chấm tuần tự) | |
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
