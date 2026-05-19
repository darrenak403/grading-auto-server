# PRN232 Auto Grader

Hệ thống chấm thi tự động cho môn PRN232 (lập trình ASP.NET). Hỗ trợ 2 dạng câu hỏi: **Q1** (SQL/Stored Procedures) và **Q2** (ASP.NET Razor API). Chấm bài theo luồng bất đồng bộ qua RabbitMQ.

---

## Mục lục

- [Tổng quan kiến trúc](#tổng-quan-kiến-trúc)
- [Tech Stack](#tech-stack)
- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Cài đặt & Chạy](#cài-đặt--chạy)
  - [Chế độ Development](#chế-độ-development)
  - [Chế độ Production](#chế-độ-production)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Các luồng chính của hệ thống](#các-luồng-chính-của-hệ-thống)
- [API Endpoints](#api-endpoints)
- [Biến môi trường](#biến-môi-trường)

---

## Tổng quan kiến trúc

```
┌──────────────────────────────────────────────────────────────┐
│                        Frontend                              │
│                   Next.js 16 (port 3000)                     │
│              REST API calls (/api/v1/...)                    │
└───────────────────────────┬──────────────────────────────────┘
                            │ HTTP/REST
┌───────────────────────────▼──────────────────────────────────┐
│                      Backend API                             │
│               ASP.NET Core 8 (port 5049/8080)                │
│           Clean Architecture: Domain → App → Infra → API     │
└───────────┬──────────────────────────┬───────────────────────┘
            │ EF Core                  │ MassTransit / AMQP
            │                          │
┌───────────▼──────────┐  ┌────────────▼───────────────────────┐
│     PostgreSQL 16    │  │           RabbitMQ 3               │
│  (dữ liệu chính)     │  │    (hàng đợi job chấm thi)         │
└──────────────────────┘  └────────────┬───────────────────────┘
                                        │ consume
┌───────────────────────────────────────▼───────────────────────┐
│                        Worker Service                         │
│            .NET 8 Background Worker (GradingPipeline)         │
│   Chạy artifact → test cases → lưu kết quả → dọn dẹp         │
└───────────┬───────────────────────────────────────────────────┘
            │ kết nối tạm (Q1)
┌───────────▼──────────┐
│     SQL Server 2022  │
│  (chạy Q1 database   │
│   của sinh viên)     │
└──────────────────────┘
```

---

## Tech Stack

| Thành phần         | Công nghệ                                              |
| ------------------ | ------------------------------------------------------ |
| Frontend           | Next.js 16.2.3, React 19, TypeScript 5, Tailwind CSS 4 |
| Backend API        | ASP.NET Core 8, EF Core 8, API Versioning              |
| Worker             | .NET 8 Background Service, MassTransit 8.3.6           |
| Message Broker     | RabbitMQ 3                                             |
| Database chính     | PostgreSQL 16                                          |
| Database sinh viên | SQL Server 2022 (Q1)                                   |
| Storage            | Local filesystem `/storage`                            |
| Container          | Docker & Docker Compose                                |
| Browser Automation | Microsoft.Playwright 1.52                              |
| Excel Export       | EPPlus 7                                               |

---

## Yêu cầu hệ thống

- **Docker Desktop** >= 24.x (bật trước khi chạy compose)
- **Docker Compose** >= 2.x
- **.NET SDK 8.0+** (dev: chạy API/Worker trên máy)
- **[Task](https://taskfile.dev/)** (tùy chọn, khuyến nghị cho lệnh setup)
- RAM tối thiểu: **4 GB** (khuyến nghị 8 GB)
- Disk: **10 GB** trống

Dev local: [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md) · User (Docker full stack): [`RUN_FOR_USER.md`](RUN_FOR_USER.md)

---

## Cài đặt & Chạy

### Bước 1 — Cấu hình biến môi trường

```bash
cp .env.example .env
```

Chỉnh sửa `.env` theo môi trường của bạn (xem phần [Biến môi trường](#biến-môi-trường)).

---

### Chế độ Development

Trong chế độ dev, chỉ chạy **infrastructure** bằng Docker. **API** và **Worker** chạy trực tiếp trên máy (`dotnet run`). Repo này không chứa mã frontend — UI web lấy từ image Docker khi deploy production.

Cần `.env` trước (`cp .env.example .env`). Chi tiết: [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md).

| Giai đoạn | Lệnh |
|-----------|------|
| Lần đầu | `task dev:setup` |
| Hằng ngày | `task dev:up` → `task dev:api` + `task dev:worker` |
| Tắt | `task dev:down` |

**Thủ công — infrastructure:**

```bash
docker compose -f docker-compose.dev.yml up -d
```

| Service | Port | Ghi chú |
|---------|------|---------|
| PostgreSQL 16 | 5432 | DB chính |
| SQL Server 2022 | 1433 | DB Q1 sinh viên |
| RabbitMQ | 5672 (AMQP), 15672 (UI) | Message broker |
| pgWeb (profile `tools`) | 8081 | `docker compose -f docker-compose.dev.yml --profile tools up -d` |

RabbitMQ Management UI: http://localhost:15672 — đăng nhập theo `RABBITMQ_USER` / `RABBITMQ_PASSWORD` trong `.env` (mặc định `grading` / `grading_pass`).

Sau khi xóa volume dev: `task dev:reset` (xem [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md)).

---

### Chế độ Production

Chỉ Docker — xem [`RUN_FOR_USER.md`](RUN_FOR_USER.md).

| Giai đoạn | Lệnh |
|-----------|------|
| Lần đầu | `task user:setup` |
| Hằng ngày | `task user:up` |
| Tắt | `task user:down` |

| Service     | Port (mặc định `.env`) | URL |
| ----------- | ---------------------- | --- |
| Frontend    | 3000                   | http://localhost:3000 |
| API         | 5049 → container 8080  | http://localhost:5049/swagger |
| RabbitMQ UI | 15672                  | http://localhost:15672 |
| pgWeb       | 8081                   | http://localhost:8081 |

Dừng: `task user:down`

---

## Cấu trúc thư mục

```
grading-auto-server/
├── GradingSystem.Api/                   # ASP.NET Core Web API
│   ├── Controllers/
│   ├── appsettings.json
│   └── Program.cs
├── GradingSystem.Worker/                # Background worker
│   └── Services/                        # GradingPipeline, ArtifactRunner, ...
├── GradingSystem.Application/
├── GradingSystem.Domain/
├── GradingSystem.Infrastructure/        # EF Core + migrations
├── supports/given/givenAPI/             # API mẫu Q2
├── storage/                             # Upload local (gitignored, tạo khi setup)
├── docker-compose.dev.yml
├── docker-compose.prod.yml
├── Project.sln
├── Taskfile.yml
├── RUN_FOR_USER.md                      # User: Docker full stack
├── RUN_FOR_DEV.md                       # Developer: infra Docker + dotnet run
└── .env.example
```

---

## Các luồng chính của hệ thống

### Luồng 1 — Tạo đề thi & câu hỏi

```
Giảng viên
  │
  ├─► Tạo Assignment (mã đề, tiêu đề)
  │
  ├─► Upload resources:
  │     • Q1: file database.sql (tạo DB mẫu trên SQL Server)
  │     • Q2: URL API mẫu đã chạy (GivenApiBaseUrl)
  │
  ├─► Tạo Questions (loại Api/Razor, điểm tối đa)
  │
  └─► Tạo TestCases cho từng Question
        • HTTP method, URL template
        • Input JSON (body/params)
        • Expected JSON (status code, response body, isArray)
        • Điểm của test case này
```

### Luồng 2 — Nộp bài & chấm thi

```
Giảng viên
  │
  ├─► Import danh sách sinh viên (CSV)
  │
  ├─► Upload master.zip (chứa tất cả bài nộp)
  │     Cấu trúc zip: {studentCode}/{artifact}/
  │
  ├─► Trigger Grade → API tạo GradingJob → publish GradeJobMessage lên RabbitMQ
  │
  └─► Worker nhận message:
        1. Load job từ PostgreSQL
        2. Lock assignment (tránh race condition SQL Server)
        3. Mark job = Running
        4. ArtifactRunner: extract zip, chạy API artifact của SV
        5. TestRunner: gọi từng test case HTTP, so sánh response với expected
        6. Lưu QuestionResult (điểm, chi tiết từng test case)
        7. Mark job = Done / Failed
        8. Cleanup: xóa artifact zip, dừng process
```

### Luồng 3 — Xem kết quả & điều chỉnh điểm

```
Giảng viên
  │
  ├─► Xem danh sách Submissions của Assignment
  │
  ├─► Xem chi tiết Submission → QuestionResults
  │     • Điểm tự động (Score)
  │     • Chi tiết từng test case (pass/fail, response nhận được)
  │
  ├─► Điều chỉnh điểm thủ công nếu cần (AdjustedScore + lý do)
  │
  └─► Thêm ReviewNote cho sinh viên
```

### Luồng 4 — Xuất kết quả Excel

```
Giảng viên
  │
  ├─► Tạo ExportJob (theo Assignment hoặc ExamSession)
  │     • Lọc theo GradingRound (tùy chọn)
  │
  ├─► Worker xử lý ExportJob async
  │     • Tổng hợp điểm từ QuestionResults
  │     • Dùng EPPlus tạo file .xlsx (multi-sheet cho session)
  │
  └─► Download file Excel khi ExportJob = Done
```

### Luồng 5 — Quản lý kỳ thi (ExamSession)

```
Giảng viên
  │
  ├─► Tạo ExamSession (nhóm nhiều Assignment)
  │
  ├─► Gán Assignments vào session
  │
  ├─► Xem kết quả tổng hợp toàn kỳ thi
  │     • Điểm từng Assignment của từng sinh viên
  │
  └─► Xuất Excel toàn kỳ (multi-sheet, 1 sheet/assignment)
```

---

## API Endpoints

Base URL: `http://localhost:5049/api/v1`  
Swagger UI: `http://localhost:5049/swagger`

| Controller      | Method | Path                                    | Mô tả                |
| --------------- | ------ | --------------------------------------- | -------------------- |
| Assignments     | POST   | `/assignments`                          | Tạo đề thi           |
|                 | GET    | `/assignments`                          | Danh sách đề thi     |
|                 | GET    | `/assignments/{id}`                     | Chi tiết đề thi      |
|                 | PUT    | `/assignments/{id}/resources`           | Upload SQL + API URL |
|                 | POST   | `/assignments/{id}/participants/import` | Import sinh viên CSV |
|                 | POST   | `/assignments/{id}/bulk-upload`         | Upload master.zip    |
|                 | POST   | `/assignments/{id}/grade`               | Kích hoạt chấm thi   |
|                 | DELETE | `/assignments/{id}`                     | Xóa đề thi           |
| Questions       | POST   | `/assignments/{id}/questions`           | Tạo câu hỏi          |
|                 | GET    | `/assignments/{id}/questions`           | Danh sách câu hỏi    |
|                 | DELETE | `/questions/{questionId}`               | Xóa câu hỏi          |
| TestCases       | POST   | `/questions/{id}/test-cases`            | Tạo test cases       |
|                 | GET    | `/questions/{id}/test-cases`            | Danh sách test cases |
|                 | PUT    | `/test-cases/{id}`                      | Cập nhật test case   |
|                 | DELETE | `/test-cases/{id}`                      | Xóa test case        |
| Submissions     | GET    | `/assignments/{id}/submissions`         | Danh sách bài nộp    |
|                 | GET    | `/submissions/{id}`                     | Chi tiết bài nộp     |
|                 | GET    | `/submissions/{id}/results`             | Kết quả chấm         |
|                 | PUT    | `/submissions/{id}/notes`               | Thêm/sửa ghi chú     |
| GradingJobs     | GET    | `/grading-jobs/{id}`                    | Trạng thái job       |
|                 | GET    | `/submissions/{id}/grading-jobs`        | Jobs của bài nộp     |
| QuestionResults | GET    | `/question-results/{id}`                | Chi tiết kết quả     |
|                 | PUT    | `/question-results/{id}/adjust`         | Điều chỉnh điểm      |
|                 | DELETE | `/question-results/{id}/adjust`         | Xóa điều chỉnh       |
| ExamSessions    | POST   | `/exam-sessions`                        | Tạo kỳ thi           |
|                 | GET    | `/exam-sessions`                        | Danh sách kỳ thi     |
|                 | GET    | `/exam-sessions/{id}/results`           | Kết quả kỳ thi       |
|                 | POST   | `/exam-sessions/{id}/exports`           | Xuất Excel kỳ thi    |
| Exports         | POST   | `/exports`                              | Tạo export job       |
|                 | GET    | `/exports/{id}`                         | Trạng thái export    |
|                 | GET    | `/exports/{id}/download`                | Download file Excel  |

---

## Biến môi trường

Xem file `.env.example` để biết đầy đủ các biến. Các biến quan trọng:

```env
# PostgreSQL
POSTGRES_DB=grading_system
POSTGRES_USER=postgres
POSTGRES_PASSWORD=grading_pass

# SQL Server (Q1 grading)
SA_PASSWORD=YourStrong@Passw0rd

# RabbitMQ (docker-compose.dev.yml / prod)
RABBITMQ_USER=grading
RABBITMQ_PASSWORD=grading_pass

# API
ASPNETCORE_ENVIRONMENT=Development
STORAGE_PATH=/storage

# Frontend
NEXT_PUBLIC_API_URL=http://localhost:5049
NEXT_PUBLIC_APP_URL=http://localhost:3000
```

---

## Storage Layout

```
/storage/
├── assignments/{AssignmentId}/
│   ├── database.sql          # Template DB cho Q1
│   └── given.zip             # Source API mẫu cho Q2
├── submissions/{SubmissionId}/
│   └── artifact.zip          # Bài nộp (tự động xóa sau chấm)
└── exports/{ExportJobId}/
    └── results_{timestamp}.xlsx
```

---

## Ghi chú phát triển

- **API versioning**: Hỗ trợ v1.0 và v2.0. Mặc định dùng `v1`.
- **Request size limit**: API chấp nhận request tối đa **200 MB** (cho bulk upload).
- **Concurrent grading**: Worker giới hạn **3 job đồng thời** (configurable).
- **Score logic**: `FinalScore = AdjustedScore ?? AutoScore` — điểm thủ công ưu tiên hơn điểm tự động.
- **Q1 isolation**: Mỗi bài Q1 tạo một database tạm trên SQL Server, xóa sau khi chấm xong.
