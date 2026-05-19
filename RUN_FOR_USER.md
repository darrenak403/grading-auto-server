# Chạy local cho User (giảng viên)

Chỉ **Docker Desktop** — Frontend, API, Worker, DB đều trong container. **Không** cần .NET, **không** `dev:api` / `dev:worker`.

Developer: [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md).

---

## Yêu cầu

- Docker Desktop
- File `.env` (**một lần, thủ công**):

```bash
cp .env.example .env
```

Windows: `copy .env.example .env` hoặc `Copy-Item .env.example .env`

---

## Ba lệnh chính

| Giai đoạn | Lệnh |
|-----------|------|
| **Lần đầu** | `task user:setup` |
| **Hằng ngày** | `task user:up` |
| **Tắt** | `task user:down` |

Gõ `task` để xem gợi ý nhanh.

### Lần đầu — `task user:setup`

Pull image và chạy full stack. Đợi **2–3 phút** (lần đầu có thể lâu hơn). API/Worker **tự migrate** DB khi khởi động.

| URL | |
|-----|---|
| Web | http://localhost:3000 |
| Swagger | http://localhost:5049/swagger |
| RabbitMQ | http://localhost:15672 (`grading` / `grading_pass`) |

### Hằng ngày

Bật Docker Desktop → `task user:up` → đợi 1–2 phút → mở http://localhost:3000

**Không** cần `user:setup` lại.

### Tắt

```bash
task user:down
```

Dữ liệu **giữ nguyên** trong Docker volume.

---

## Xóa container / image / volume

| Đã xóa | Data | Làm lại |
|--------|------|---------|
| Container / image app | Còn | `task user:up` |
| Image DB (postgres, sqlserver) | Còn | `task user:up` (tải lại image) |
| **Volume** | Mất | `task user:up` — DB trống, tự migrate |
| Máy mới | — | `.env` + `task user:setup` |

Log khi lỗi: `docker compose -f docker-compose.prod.yml logs -f api worker`

---

## Không dùng Task

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
# Tắt: docker compose -f docker-compose.prod.yml down
```

---

## Lỗi thường gặp

**Docker chưa chạy** — mở Docker Desktop, thử lại.

**Trang web chưa vào được** — đợi thêm 2–3 phút; xem log ở trên.

**Grade báo lỗi path / không tìm thấy zip** — upload lại `master.zip` trước khi Grade (Worker xóa artifact sau lần chấm lỗi).
