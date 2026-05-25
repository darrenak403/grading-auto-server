# Docker

| File | Mục đích |
|------|----------|
| `.env.example` | Mẫu biến |
| `.env.local` | Dev (gitignored) |
| `.env` | Prod / Dokploy (gitignored) |
| `docker-compose.dev.yml` | Dev: infra + profile `app` (chỉ dùng cho `docker:build`) |
| `docker-compose.prod.yml` | Full stack + `pull_policy: always` |

Hướng dẫn: [`../BE.md`](../BE.md)

**Dev:** `task docker:up` → infra. App chạy `task run` + `task run:worker`.  
`task docker:build` build image `api`/`worker` (profile `app`, không tự `up`).

**Prod:** `DOCKER_REPO` = Docker Hub user (= `DOCKER_USERNAME` trong CI).
