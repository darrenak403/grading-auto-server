# Chạy Backend (`be/`)

Tổng quan: [`README.md`](README.md). FE **repo riêng**.

Tạo `docker/.env.local` từ `docker/.env.example`.

```bash
task docker:up
task run          # terminal 1
task run:worker   # terminal 2
```

Swagger: http://localhost:5049/swagger

| Lệnh | Mô tả |
|------|--------|
| `docker:up` | Infra (`up --no-build`) |
| `docker:build` | Build image API + Worker (đổi code / test image local) |
| `docker:down` | Tắt infra |
| `docker:logs` | Xem log infra |
| `run` | API (`dotnet run`) |
| `run:worker` | Worker (`dotnet run`) |

`appsettings.Development.json` — `localhost` + **`POSTGRES_PUBLISH_PORT`** trong `.env.local` (vd. `5439`).

---

## Prod / CI

Prod: `docker/docker-compose.prod.yml` + `docker/.env`, deploy **Dokploy**.  
CI: [`.github/workflows/ci-cd.yml`](.github/workflows/ci-cd.yml) (không dùng Task).
