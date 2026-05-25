# Chạy Backend (`be/`)

Tổng quan: [`README.md`](README.md). FE **repo riêng**.

Tạo `docker/.env.local` tay (mẫu: `docker/.env.example`). `CORS_ORIGIN_*` trỏ URL FE.

---

## Docker (API + Worker trong container)

```bash
task docker:build    # sau khi sửa code C#
task docker:up
```

Image từ registry: `task docker:pull` → `task docker:up`.

| URL | Mặc định |
|-----|----------|
| Swagger | http://localhost:5049/swagger |
| RabbitMQ UI | http://localhost:15672 |

`task docker:down` · `task docker:logs`

---

## Local (dotnet trên máy)

```bash
task docker:up
docker compose --env-file docker/.env.local -f docker/docker-compose.dev.yml stop api worker
```

Hai terminal: `task run` · `task run:worker`

Swagger: http://localhost:5049/swagger

---

## Sau khi sửa code

| Cách | Lệnh |
|------|------|
| Docker | `task docker:build` → `task docker:up` |
| Local | Restart `task run` / `task run:worker` |

Pull image mới: `task docker:pull` → `task docker:up`

---

## CI / CD

| Bước | Công cụ |
|------|---------|
| CI (push `main`) | GitHub Actions [`.github/workflows/ci-docker.yml`](.github/workflows/ci-docker.yml) — build/push Docker Hub |
| CD (VPS) | **Dokploy** — pull image và deploy |

Secrets repo CI: `DOCKER_USERNAME`, `DOCKER_PASSWORD`.  
Image: `{DOCKER_USERNAME}/grading-system-{api,worker}:latest` (và `:sha`).

---

## Lệnh Task

| Lệnh | Mô tả |
|------|--------|
| `run` | API local |
| `run:worker` | Worker local |
| `docker:build` | Build image |
| `docker:up` / `docker:down` / `docker:pull` / `docker:logs` | Container |
