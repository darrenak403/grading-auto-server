# Hướng dẫn Cài đặt & Chạy ứng dụng cho Developer (Môi trường Dev)

Tài liệu này dành cho các lập trình viên (Developer) muốn chạy Backend ở môi trường Local để code, debug và test.

## 1. Yêu cầu hệ thống

| Công cụ | macOS | Windows |
|--------|-------|---------|
| **Docker Desktop** | Bắt buộc | Bắt buộc |
| **.NET 8 SDK** | `brew install dotnet@8` hoặc [dotnet.microsoft.com](https://dotnet.microsoft.com/download) | Cài từ installer / `winget install Microsoft.DotNet.SDK.8` |
| **Task** (khuyến nghị) | `brew install go-task` | `winget install Task.Task` hoặc [taskfile.dev](https://taskfile.dev/installation/) |
| **Node.js 18+** | Chỉ khi chạy Frontend | Chỉ khi chạy Frontend |

IDE khuyên dùng: Visual Studio 2022, JetBrains Rider, hoặc VS Code.

> **Windows:** Mở terminal tại thư mục `be` (PowerShell hoặc CMD). Task chạy lệnh native trên Windows, không cần Git Bash.

## 2. Quick start (Mac & Windows)

Tại thư mục `be`:

```bash
# Lần đầu (restore + infra + Playwright + migrate) — xem RUN_FOR_DEV.md
task dev:setup

# Mỗi ngày
task dev:up
task dev:api      # terminal 1 → http://localhost:5049/swagger
task dev:worker   # terminal 2
```

Chi tiết: [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md). Nếu Docker vừa tắt: `task dev:up` trước api/worker.

### 2.1. Playwright lỗi khi `task dev:setup`?

Worker dùng **Playwright** (.NET). Bắt buộc **build project trước**, rồi mới cài Chromium.

Chạy lại riêng bước Playwright:

```bash
cd be
task _dev:playwright
```

Hoặc thủ công:

```bash
cd GradingSystem.Worker
dotnet tool install --global Microsoft.Playwright.CLI
dotnet build -c Debug
# macOS/Linux:
./bin/Debug/net8.0/playwright.sh install chromium
# Windows (PowerShell) — dùng / hoặc \ đều được trong PowerShell:
powershell -NoProfile -ExecutionPolicy Bypass -File ./bin/Debug/net8.0/playwright.ps1 install chromium
```

> **Mac:** Nếu không có `playwright.sh`, cài PowerShell: `brew install powershell`, rồi chạy lại `task _dev:playwright`.

> **Windows:** Đảm bảo `%USERPROFILE%\.dotnet\tools` có trong PATH (mở terminal mới sau khi cài .NET SDK).

## 3. Khởi chạy thủ công (không dùng Task)

### 3.1. Hạ tầng Docker

```bash
cp .env.example .env          # Mac/Linux
# copy .env.example .env        # Windows CMD
docker compose -f docker-compose.dev.yml up -d
```

Container: `grading-postgres`, `grading-sqlserver`, `grading-rabbitmq`.

> **Mac chip M:** SQL Server chạy `linux/amd64` — Docker Desktop tự emulation.

### 3.2. API & Worker (2 terminal)

```bash
cd GradingSystem.Api && dotnet run    # http://localhost:5049/swagger
cd GradingSystem.Worker && dotnet run
```

### 3.3. Migrate DB (lần đầu)

```bash
cd GradingSystem.Api
dotnet ef database update --project ../GradingSystem.Infrastructure --startup-project .
```

## 4. Quản lý Hạ tầng Dev

- Nếu muốn xem **PGWeb** (Giao diện web để quản lý Database Postgres), bạn chạy lệnh kèm profile `tools`:

  ```bash
  docker compose -f docker-compose.dev.yml --profile tools up -d
  ```

  Sau đó vào: `http://localhost:8081`

- Để **tắt hạ tầng** khi kết thúc ngày làm việc (giữ nguyên dữ liệu):

  ```bash
  docker compose -f docker-compose.dev.yml stop
  ```

- Để **xóa hoàn toàn** Database (reset lại dữ liệu sạch từ đầu):
  ```bash
  docker compose -f docker-compose.dev.yml down -v
  ```
