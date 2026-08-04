# PRN232 Auto Grader

Hệ thống chấm thi tự động cho môn PRN232 (ASP.NET). Hỗ trợ **Q1** (SQL/Stored Procedures) và **Q2** (ASP.NET Razor API). Chấm bài bất đồng bộ qua RabbitMQ.

## Chạy nhanh backend

```bash
cp docker/.env.example docker/.env.local   # lần đầu: tạo cấu hình local
# Trong docker/.env.local, bỏ comment:
# Playwright__BrowserCdpEndpoint=http://127.0.0.1:9222
task up                                     # khởi động hạ tầng
task run                                    # Playwright + API + Worker
```

Swagger: http://localhost:5049/swagger

Chi tiết cấu hình và các lệnh chạy riêng: [`BE.md`](BE.md).

---

## Mục lục

- [Chạy nhanh backend](#chạy-nhanh-backend)
- [Kiến trúc](#kiến-trúc)
- [Tech stack](#tech-stack)
- [Cấu trúc repo](#cấu-trúc-repo)
- [Luồng nghiệp vụ](#luồng-nghiệp-vụ)
- [Quy trình chấm PE](#quy-trình-chấm-pe)
- [Quy trình chấm Lab](#quy-trình-chấm-lab)
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

Import CSV sinh viên → upload `master.zip` (thêm vào round hiện tại) hoặc tạo round mới → **Grade** → API publish message → Worker: extract artifact, chạy test, lưu `QuestionResult`, cleanup. Mỗi round được đánh số tự động ("Lần 1", "Lần 2", ...) và độc lập với nhau.

### Kết quả & export

Xem submission/results, chỉnh `AdjustedScore`, ReviewNote → ExportJob → Excel (EPPlus).

### Kỳ thi (ExamSession)

Gom nhiều Assignment, xem kết quả tổng hợp, export multi-sheet.

---

## Quy trình chấm PE

PE (Practical Exam) chấm theo **Assignment → Question → TestCase**, có thể gom nhiều Assignment vào một **ExamSession**. Chấm nhiều round, mỗi round độc lập.

### 1. Chuẩn bị đề

1. `POST /assignments` — tạo assignment (Q1: SQL/Stored Procedures, Q2: ASP.NET Razor API).
2. `PUT /assignments/{id}/resources` — upload `database.sql` (Q1) và/hoặc Given API `.zip` (Q2).
3. `POST /assignments/{assignmentId}/questions` — tạo từng câu hỏi.
4. `POST /questions/{questionId}/test-cases` — tạo test case cho câu hỏi (method, URL, input/expected JSON, điểm).

### 2. Import thí sinh & nộp bài

5. `POST /assignments/{id}/participants/import` — import danh sách thí sinh (CSV: mã SV).
6. Nộp bài — chọn một trong hai:
   - `POST /assignments/{id}/bulk-upload` — upload `master.zip`, thêm submission vào round **hiện tại**.
   - `POST /assignments/{id}/rounds` — tạo round mới (tự động đánh số "Lần 1", "Lần 2", ...) rồi upload vào round đó.

### 3. Chấm bài

7. `POST /assignments/{id}/grade` — trigger chấm toàn bộ assignment (round hiện tại), publish message qua RabbitMQ.
   - Worker (`GradingPipeline`): extract artifact → chạy test case (Playwright/Newman/SQL Server) → lưu `QuestionResult` → cleanup.
   - Chấm lại 1 bài lỗi: `POST /submissions/{id}/grade` (chỉ retry khi status = `Failed`).
8. Theo dõi job: `GET /grading-jobs/{id}` hoặc `GET /submissions/{submissionId}/grading-jobs`.

### 4. Xem kết quả & export

9. `GET /assignments/{assignmentId}/submissions` (filter theo round), `GET /submissions/{id}/results`.
10. Điều chỉnh điểm/ghi chú: `PUT /submissions/{id}/custom-result`, `PUT /submissions/{id}/notes`.
11. Export Excel: `POST /exports` → `GET /exports/{id}` → `GET /exports/{id}/download` (EPPlus). `FinalScore = AdjustedScore ?? AutoScore`.

### 5. (Tuỳ chọn) Gom nhiều assignment thành kỳ thi

12. `POST /exam-sessions` — tạo session, gán các assignment liên quan.
13. `POST /exam-sessions/{id}/participants/import` — import thí sinh chung cho session.
14. `GET /exam-sessions/{id}/results` — xem kết quả tổng hợp toàn kỳ thi.
15. `POST /exam-sessions/{id}/exports` — export multi-sheet (mỗi assignment một sheet).

---

## Quy trình chấm Lab

Lab chấm theo **LabAssignment → LabTestCase**, không có Question — mỗi lab là một bài chấm độc lập, hỗ trợ chấm bằng Docker Compose của sinh viên.

### 1. Chuẩn bị đề

1. `POST /lab-assignments` — tạo lab assignment (gắn semester).
2. `POST /lab-assignments/{id}/testcases` — tạo test case đơn, hoặc `POST /lab-assignments/{id}/testcases/batch` — import hàng loạt (JSON array, thường sinh bằng AI theo `docs/testcase-prompt-template.md`).
   - Test case thường (`HTTP`): gọi API sinh viên chấm theo `httpMethod`, `urlTemplate`, `inputJson`/`expectJson`.
   - Test case `SOURCE`: kiểm tra mã nguồn (không cần chạy Docker) — `httpMethod = "SOURCE"`, `urlTemplate = "rule:args"` với rule `project-count:N`, `project-name:GLOB`, `folder-exists:GLOB`, `file-exists:GLOB`, `file-contains:GLOB:TEXT`, `file-not-contains:GLOB:TEXT`.
3. `PATCH /lab-assignments/{id}/testcases/approve-all` — duyệt hàng loạt test case ở trạng thái `Draft` → `Approved` (hoặc duyệt từng cái: `PATCH /lab-testcases/{id}/status`).

### 2. Nộp bài

4. Nộp từng bài: `POST /lab-submissions?assignmentId={id}` — multipart upload, tên file theo convention **`Lab{N}_{MaSV}.zip`** (vd. `Lab1_SE180234.zip`).
   - Hoặc nộp hàng loạt: `POST /lab-assignments/{assignmentId}/bulk-upload`.
5. `GET /lab-assignments/{id}/roster` — đối chiếu danh sách sinh viên đã nộp/chưa nộp.

### 3. Chấm bài

6. `POST /lab-assignments/{id}/grade` — trigger chấm toàn bộ submission Pending của lab (bỏ qua job đang `Pending`/`Running`), hoặc `POST /lab-assignments/{id}/grade-all` — chấm lại toàn bộ kể cả đã chấm.
   - Worker (`LabGradingWorker`, polling 5s + `LabGradeJobConsumer` qua RabbitMQ/MassTransit):
     1. Extract ZIP/RAR.
     2. Chạy SOURCE test case trước (không phụ thuộc Docker).
     3. `docker compose up` bằng compose file của sinh viên → strip toàn bộ `ports:` gốc, tự động detect service API + port (fallback `api:8080`) → gán host port trong dải **15000–16000** → chạy HTTP test case → `docker compose down`.
     4. Lưu `LabTestCaseResult`.
   - Yêu cầu hạ tầng: pre-pull base image Docker trên máy chấm (vd. `docker pull mcr.microsoft.com/dotnet/aspnet:8.0`) để tránh lỗi `BuildFailed` do mạng.
7. Chấm lại 1 bài: `POST /lab-submissions/{id}/regrade`; chấm lại toàn bộ lab: `POST /lab-submissions/regrade-all?assignmentId={id}`.
8. Theo dõi tiến độ: `GET /lab-assignments/{id}/grading-progress`.

### 4. Xem kết quả & export

9. `GET /lab-submissions?assignmentId={id}` (sort theo mã SV), `GET /lab-submissions/{id}/results`.
10. Điều chỉnh điểm: `PUT /lab-submissions/{id}/adjust`; sửa kết quả test case cụ thể: `PUT /lab-submissions/{id}/custom-result`.
11. Export: `POST /lab-assignments/{id}/exports`.
12. (Tuỳ chọn) Đồng bộ điểm lên Supabase: `POST /lab-assignments/{id}/sync-supabase` (toàn bộ) hoặc `POST /lab-assignments/sync-supabase-grade(s)` (từng bài/hàng loạt).

---

## API

Base: `http://localhost:5049/api/v1` · Swagger: `http://localhost:5049/swagger`

| Nhóm | Ví dụ |
|------|--------|
| Assignments | `POST /assignments`, `POST .../grade` |
| Bulk Upload & Rounds | `POST .../bulk-upload` (thêm vào round hiện tại), `POST .../rounds` (tạo round mới auto-numbered) |
| Questions / TestCases | CRUD theo assignment / question |
| Submissions / Results | `GET .../submissions` (filter by round), `POST .../grade` (retry chỉ khi status=Failed), `PUT .../adjust` |
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
- **Worker concurrency:**
  - `MaxConcurrentJobs`: Mặc định = `Math.Clamp(CoreCount - 1, 1, 8)` (tự động theo CPU).
    Cấu hình trong `.env` qua `WORKER_MAX_CONCURRENT_JOBS` hoặc appsettings.json (`Worker:MaxConcurrentJobs`).
  - `SubmissionTimeoutSeconds`: Mặc định = **90 giây**. Cấu hình qua `WORKER_SUBMISSION_TIMEOUT_SECONDS`.
    Timeout này áp dụng toàn bộ quá trình chấm một bài (artifact run + test execution); vượt quá sẽ kill process và mark Failed.
- Điểm: `FinalScore = AdjustedScore ?? AutoScore`.
- Q1: database tạm trên SQL Server, xóa sau khi chấm.
