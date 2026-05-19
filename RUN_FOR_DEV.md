# Chạy local cho Developer

Docker chạy **DB + RabbitMQ**; **API** và **Worker** chạy trên máy (`dotnet run`).

User chỉ Docker (không code): [`RUN_FOR_USER.md`](RUN_FOR_USER.md).

---

## Yêu cầu

- Docker Desktop (bật trước khi chạy task)
- .NET SDK 8+
- [Task](https://taskfile.dev/installation/)
- File `.env` (copy từ `.env.example` — **làm một lần, thủ công**)

```bash
cp .env.example .env
```

Windows: `copy .env.example .env` hoặc `Copy-Item .env.example .env`

---

## Ba lệnh chính

| Giai đoạn | Lệnh |
|-----------|------|
| **Lần đầu** | `task dev:setup` |
| **Hằng ngày** | `task dev:up` → `task dev:api` (terminal 1) + `task dev:worker` (terminal 2) |
| **Tắt** | `task dev:down` |

Gõ `task` (không tham số) để xem gợi ý nhanh.

### Lần đầu — `task dev:setup`

Restore NuGet, tạo `storage/`, bật infra Docker, cài Playwright, migrate PostgreSQL. Chỉ chạy **một lần** trên máy (hoặc sau khi xóa volume).

### Hằng ngày

```bash
task dev:up
task dev:api      # terminal 1 — http://localhost:5049/swagger
task dev:worker   # terminal 2
```

> Task tự set `ASPNETCORE_ENVIRONMENT=Development` (`.env` chỉ dùng cho Docker). Nếu chạy `dotnet run` trực tiếp, dùng `Properties/launchSettings.json` trong project.

**Không** chạy lại `dev:setup`.

### Tắt cuối ngày

```bash
task dev:down
```

Dữ liệu DB **giữ nguyên** (Docker volume).

---

## Xóa container / image / volume

| Đã xóa | Data | Làm lại |
|--------|------|---------|
| Container hoặc image | Còn | `task dev:up` → api + worker |
| **Volume** (`down -v`, prune volumes) | Mất | `task dev:reset` (hoặc `dev:setup` từ đầu) |
| Máy mới | — | `.env` + `task dev:setup` |

Kiểm tra container: `docker compose -f docker-compose.dev.yml ps`

---

## Không dùng Task (thủ công)

<details>
<summary>Click để mở các bước thủ công</summary>

```bash
dotnet restore Project.sln
mkdir -p storage
docker compose -f docker-compose.dev.yml up -d
# Playwright: dotnet tool install --global Microsoft.Playwright.CLI && playwright install chromium
cd GradingSystem.Api
dotnet ef database update --project ../GradingSystem.Infrastructure --startup-project .
cd ..
# Terminal 1: cd GradingSystem.Api && dotnet run
# Terminal 2: cd GradingSystem.Worker && dotnet run
```

</details>
