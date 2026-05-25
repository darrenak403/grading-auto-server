# Chạy Backend (`be/`)

Tổng quan: [`README.md`](README.md). FE **repo riêng**.

---

## Cấu hình

Tự tạo env (tham khảo `docker/.env.example`):

| Môi trường | File |
|------------|------|
| Dev | `docker/.env.local` |
| Prod | `docker/.env` |

`CORS_ORIGIN_*` trỏ URL FE (vd. `http://localhost:3000`).

---

## Dev — Docker (API + Worker trong container)

```bash
task docker:build    # sau khi sửa code C#
task docker:up       # bật container
```

Lần đầu / image trên registry: `task docker:pull` rồi `docker:up`.

| URL | Mặc định |
|-----|----------|
| Swagger | http://localhost:5049/swagger |
| RabbitMQ UI | http://localhost:15672 |

`task docker:down` · `task docker:logs`

---

## Dev — Local (dotnet trên máy)

DB/RabbitMQ/SQL Server vẫn qua Docker:

```bash
task docker:up
docker compose --env-file docker/.env.local -f docker/docker-compose.dev.yml stop api worker
```

Hai terminal:

```bash
task run
task run:worker
```

Swagger: http://localhost:5049/swagger

`appsettings.Development.json` trỏ `localhost` + port trong `.env.local`.

---

## Sau khi sửa code

| Cách chạy | Lệnh |
|-----------|------|
| Docker | `task docker:build` → `task docker:up` |
| Local | Restart `task run` / `task run:worker` |

Chỉ đổi image từ registry: `task docker:pull` → `task docker:up`.

---

## Prod / VPS

```bash
task docker:prod:up
```

(`docker/.env` tạo tay từ `.env.example`)

---

## Lệnh Task

| Lệnh | Mô tả |
|------|--------|
| `run` | API local (`dotnet run`) |
| `run:worker` | Worker local |
| `docker:build` | Build image API + Worker |
| `docker:up` / `docker:down` / `docker:pull` / `docker:logs` | Container dev |
| `docker:prod:up` / `docker:prod:down` / `docker:prod:pull` | Prod |
