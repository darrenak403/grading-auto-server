# Chạy local cho User (giảng viên / vận hành)

Chỉ cần **Docker Desktop**. Toàn bộ **Frontend, API, Worker, Database** chạy trong container — **không** cần cài .NET, **không** cần `task api` hay `task worker`.

Developer sửa code: [`RUN_FOR_DEV.md`](RUN_FOR_DEV.md).

---

## Yêu cầu

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (Mac hoặc Windows)
- RAM khuyến nghị **8 GB**, disk trống **~10 GB** (lần đầu tải image SQL Server ~500MB+)
- [Task](https://taskfile.dev/installation/) — tùy chọn; có thể chỉ dùng `docker compose`

---

## Cách 1 — Một lệnh (có Task)

```bash
git clone https://github.com/darrenak403/grading-auto-server.git
cd grading-auto-server
```

**Bước 1 — Tạo `.env` thủ công:**

```bash
cp .env.example .env
```

(Windows: `copy .env.example .env` hoặc `Copy-Item .env.example .env`)

**Bước 2 — Bật Docker Desktop**, rồi:

```bash
task setup:user
```

Đợi **2–3 phút** (lần đầu có thể lâu hơn khi pull image). API/Worker tự **migrate database** khi khởi động.

### Truy cập

| Dịch vụ | URL (mặc định `.env`) |
|---------|------------------------|
| **Giao diện web** | http://localhost:3000 |
| **Swagger API** | http://localhost:5049/swagger |
| **RabbitMQ** | http://localhost:15672 — `grading` / `grading_pass` |

Dừng hệ thống:

```bash
task prod:down
```

---

## Cách 2 — Từng bước (không cần Task)

### Bước 0 — Clone

```bash
git clone https://github.com/darrenak403/grading-auto-server.git
cd grading-auto-server
```

### Bước 1 — File `.env`

**macOS / Linux / Git Bash:**

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

(Có thể giữ mặc định; chỉnh port/password nếu cần.)

### Bước 2 — Bật Docker Desktop

Đảm bảo Docker đang chạy (icon whale không báo lỗi).

### Bước 3 — Tải image & khởi động stack

```bash
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

### Bước 4 — Kiểm tra container

```bash
docker compose -f docker-compose.prod.yml ps
```

Tất cả service nên ở trạng thái **running** (postgres/sqlserver/rabbitmq có thể cần thêm vài phút để **healthy**).

### Bước 5 — Mở trình duyệt

- http://localhost:3000 — UI chấm thi  
- http://localhost:5049/swagger — API  

Dừng:

```bash
docker compose -f docker-compose.prod.yml down
```

---

## Cách 3 — Chỉ tải 2 file (không clone repo)

Tạo thư mục trống, mở terminal tại đó.

**macOS / Linux:**

```bash
curl -fsSL -o docker-compose.prod.yml https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/docker-compose.prod.yml
curl -fsSL -o .env https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/.env.example
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

**Windows PowerShell:**

```powershell
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/docker-compose.prod.yml" -OutFile "docker-compose.prod.yml"
Invoke-WebRequest -Uri "https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/.env.example" -OutFile ".env"
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

---

## Lỗi thường gặp

### Docker không chạy

Thông báo kiểu `dockerDesktopLinuxEngine` → mở **Docker Desktop**, thử lại.

### Trang web/API chưa vào được

Lần đầu SQL Server + migrate có thể mất **3–5 phút**. Xem log:

```bash
docker compose -f docker-compose.prod.yml logs -f api worker
```

### Lỗi `The value cannot be an empty string. (Parameter 'path')` khi Grade

Worker đã xóa `artifact.zip` sau lần chấm lỗi. **Upload lại** `master.zip` rồi mới bấm Grade.

### Cấu trúc zip bài sinh viên

Giải nén phải thấy ngay `.dll` hoặc thư mục có `.csproj` — tránh bọc quá nhiều cấp thư mục.

### Mac chip Apple Silicon

SQL Server trong compose dùng `linux/amd64` — Docker Desktop tự emulate; lần đầu pull có thể chậm.

---

## Port mặc định (trong `.env`)

| Biến | Mặc định | Dịch vụ |
|------|----------|---------|
| `FE_PORT` | 3000 | Frontend |
| `API_PORT` | 5049 | API (map vào container 8080) |
| `RABBITMQ_UI_PORT` | 15672 | RabbitMQ UI |
| `POSTGRES_PUBLISH_PORT` | 5432 | Postgres (debug) |

Image: `ngothanhdatak/grading-system-*:latest` (khai báo trong `.env`).
