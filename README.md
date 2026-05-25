# PRN232 Auto Grader

Hệ thống chấm thi tự động cho môn PRN232 (ASP.NET). Hỗ trợ **Q1** (SQL/Stored Procedures) và **Q2** (ASP.NET Razor API). Chấm bài bất đồng bộ qua RabbitMQ.

**Chạy backend:** xem [`BE.md`](BE.md).

---

## Mục lục

- [Kiến trúc](#kiến-trúc)
- [Tech stack](#tech-stack)
- [Cấu trúc repo](#cấu-trúc-repo)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)
- [API](#api)
- [Storage](#storage)
- [Ghi chú kỹ thuật](#ghi-chú-kỹ-thuật)

---

## Kiến trúc

```
┌──────────────────────────────────────────────────────────────┐
│                        Frontend                              │
│                   Next.js 16 (port 3000)                     │
└───────────────────────────┬──────────────────────────────────┘
                            │ HTTP/REST
┌───────────────────────────▼──────────────────────────────────┐
│                      Backend API                               │
│               ASP.NET Core 8 (port 5049 / 8080)                │
│           Domain → Application → Infrastructure → API          │
└───────────┬──────────────────────────┬───────────────────────┘
            │ EF Core                  │ MassTransit
┌───────────▼──────────┐  ┌────────────▼───────────────────────┐
│     PostgreSQL 16    │  │           RabbitMQ 3               │
└──────────────────────┘  └────────────┬───────────────────────┘
                                        │
┌───────────────────────────────────────▼───────────────────────┐
│                     Worker (GradingPipeline)                  │
└───────────┬───────────────────────────────────────────────────┘
            │ Q1
┌───────────▼──────────┐
│     SQL Server 2022  │
└──────────────────────┘
```

---

## Tech stack

| Thành phần | Công nghệ |
|------------|-----------|
| Frontend | Next.js 16, React 19, TypeScript, Tailwind 4 |
| API | ASP.NET Core 8, EF Core 8, API versioning |
| Worker | .NET 8, MassTransit 8.3, Playwright, Newman |
| DB chính | PostgreSQL 16 |
| DB Q1 (SV) | SQL Server 2022 |
| Queue | RabbitMQ 3 |
| Deploy | Docker Compose (VPS) |

---

## Cấu trúc repo

```
be/
├── GradingSystem.Api/
├── GradingSystem.Worker/
├── GradingSystem.Application/
├── GradingSystem.Domain/
├── GradingSystem.Infrastructure/
├── supports/given/givenAPI/
├── storage/                    # gitignored
├── docker/                     # compose + .env (dev/prod)
├── Taskfile.yml
├── README.md                   # tài liệu hệ thống (file này)
└── BE.md                       # hướng dẫn chạy backend
```

---

## Luồng nghiệp vụ

### Tạo đề & test cases

Giảng viên tạo Assignment → upload `database.sql` (Q1) và/hoặc Given API (Q2) → tạo Questions → TestCases (method, URL, input/expected JSON, điểm).

### Nộp bài & chấm

Import CSV sinh viên → upload `master.zip` → **Grade** → API publish message → Worker: extract artifact, chạy test, lưu `QuestionResult`, cleanup.

### Kết quả & export

Xem submission/results, chỉnh `AdjustedScore`, ReviewNote → ExportJob → Excel (EPPlus).

### Kỳ thi (ExamSession)

Gom nhiều Assignment, xem kết quả tổng hợp, export multi-sheet.

---

## API

Base: `http://localhost:5049/api/v1` · Swagger: `http://localhost:5049/swagger`

| Nhóm | Ví dụ |
|------|--------|
| Assignments | `POST /assignments`, `POST .../grade`, `POST .../bulk-upload` |
| Questions / TestCases | CRUD theo assignment / question |
| Submissions / Results | `GET .../submissions`, `PUT .../adjust` |
| ExamSessions / Exports | session + download Excel |

Chi tiết endpoint: mở Swagger khi API đang chạy.

---

## Storage

```
storage/
├── assignments/{id}/database.sql, given.zip
├── submissions/{id}/artifact.zip
└── exports/{id}/results_*.xlsx
```

---

## Ghi chú kỹ thuật

- API versioning: v1 (mặc định), v2.
- Upload tối đa **200 MB**.
- Worker: mặc định **3** job đồng thời (cấu hình trong `.env` / appsettings).
- Điểm: `FinalScore = AdjustedScore ?? AutoScore`.
- Q1: database tạm trên SQL Server, xóa sau khi chấm.
