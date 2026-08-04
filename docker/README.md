# Docker

| File | Mục đích |
|------|----------|
| `.env.example` | Mẫu biến |
| `.env.local` | Dev (gitignored) |
| `.env` | Prod / Dokploy (gitignored) |
| `docker-compose.dev.yml` | Dev: infra + Chromium profile `playwright` |
| `docker-compose.prod.yml` | Full stack + `pull_policy: always` |

Hướng dẫn: [`../BE.md`](../BE.md)

**Dev:** `task up` khởi động infra; `task run` chạy Playwright, API và Worker.
Dùng `task playwright:down` để tắt Chromium khi không còn chấm câu Razor.

**Prod:** `DOCKER_REPO` = Docker Hub user (= `DOCKER_USERNAME` trong CI).
