# Chạy local cho Developer

Môi trường dev: **Docker** chỉ chạy DB + RabbitMQ; **API** và **Worker** chạy trên máy bằng `dotnet run` (dễ debug).

Tài liệu cho **người dùng cuối** (chỉ Docker, không cần `task api` / `task worker`): [`RUN_FOR_USER.md`](RUN_FOR_USER.md).

---

## Yêu cầu

| Công cụ | Phiên bản |
|---------|-----------|
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Bật app trước khi setup |
| [.NET SDK](https://dotnet.microsoft.com/download) | 8.0 trở lên |
| [Task](https://taskfile.dev/installation/) | Khuyến nghị (gom lệnh setup) |

Terminal: **Git Bash** (Windows), **Terminal** (macOS), hoặc shell Linux.

---

## Cách 1 — Một lệnh (khuyến nghị)

**Bước 0 — Tạo `.env` thủ công** (bắt buộc, làm trước mọi task):

**macOS / Linux / Git Bash:**

```bash
cp .env.example .env
```

**Windows CMD:** `copy .env.example .env`  
**Windows PowerShell:** `Copy-Item .env.example .env`

**Bước 1 — Setup** (thư mục gốc repo, Docker Desktop đã bật):

```bash
task setup:dev
```

Lệnh trên: restore → `storage/` → Docker infra → đợi Postgres → Playwright → migrate DB.

Sau khi xong, mỗi ngày mở **2 terminal**:

```bash
# Terminal 1
task api

# Terminal 2
task worker
```

| URL | |
|-----|---|
| Swagger | http://localhost:5049/swagger |
| RabbitMQ UI | http://localhost:15672 (user/pass trong `.env`, mặc định `grading` / `grading_pass`) |

---

## Cách 2 — Từng bước (không dùng Task)

### Bước 0 — Clone & vào thư mục

```bash
git clone https://github.com/darrenak403/grading-auto-server.git
cd grading-auto-server
```

### Bước 1 — File `.env`

**macOS / Linux / Git Bash (Windows):**

```bash
cp .env.example .env
```

**Windows CMD:**

```cmd
copy .env.example .env
```

**Windows PowerShell:**

```powershell
Copy-Item .env.example .env
```

### Bước 2 — Restore & thư mục storage

```bash
dotnet restore Project.sln
mkdir -p storage
```

Windows CMD (không có `mkdir -p`):

```cmd
mkdir storage
```

### Bước 3 — Bật Docker Desktop, rồi hạ tầng

```bash
docker compose -f docker-compose.dev.yml up -d
```

Đợi ~1 phút (SQL Server lâu hơn Postgres).

### Bước 4 — Playwright (Worker chấm UI)

```bash
dotnet tool install --global Microsoft.Playwright.CLI
```

**macOS / Linux:**

```bash
playwright install chromium
```

Nếu `playwright: command not found`:

```bash
~/.dotnet/tools/playwright install chromium
```

**Windows (CMD hoặc PowerShell):**

```cmd
%USERPROFILE%\.dotnet\tools\playwright.cmd install chromium
```

### Bước 5 — Migrate database

```bash
cd GradingSystem.Api
dotnet ef database update --project ../GradingSystem.Infrastructure --startup-project .
cd ..
```

### Bước 6 — Chạy API & Worker (2 terminal)

**Terminal 1:**

```bash
cd GradingSystem.Api
dotnet run
```

**Terminal 2:**

```bash
cd GradingSystem.Worker
dotnet run
```

---

## Lỗi thường gặp

| Triệu chứng | Cách xử lý |
|-------------|------------|
| `dockerDesktopLinuxEngine` / cannot connect to Docker | Mở **Docker Desktop**, đợi icon xanh, chạy lại bước 3 |
| `dotnet restore` — project not found | Dùng `Project.sln` ở **thư mục gốc** repo (không có `be/`) |
| EF không kết nối Postgres | `docker compose -f docker-compose.dev.yml ps` — đợi `grading-postgres` **healthy**, chạy lại bước 5 |
| Mac M1/M2 SQL Server | Compose đã set `platform: linux/amd64` — Docker tự emulate |

---

## Lệnh Task hữu ích

```bash
task --list          # xem tất cả
task infra:ps        # trạng thái container
task infra:stop      # tắt DB (giữ data)
task infra:reset     # xóa hết data dev
task db:migrate      # migrate lại
```

---

## Cấu trúc repo (tóm tắt)

```
grading-auto-server/
├── GradingSystem.Api/
├── GradingSystem.Worker/
├── GradingSystem.Application/
├── GradingSystem.Domain/
├── GradingSystem.Infrastructure/
├── docker-compose.dev.yml
├── Project.sln
├── Taskfile.yml
└── .env.example
```
