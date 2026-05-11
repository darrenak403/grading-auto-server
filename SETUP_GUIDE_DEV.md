# Hướng dẫn Cài đặt & Chạy ứng dụng cho Developer (Môi trường Dev)

Tài liệu này dành cho các lập trình viên (Developer) muốn chạy Backend ở môi trường Local để code, debug và test.

## 1. Yêu cầu hệ thống

- **Docker Desktop** (để chạy DB).
- **.NET 8 SDK** (để chạy Backend API và Worker).
- **Node.js 18+** (để chạy Frontend).
- IDE khuyên dùng: Visual Studio 2022, JetBrains Rider, hoặc VS Code.

## 2. Khởi chạy Hạ tầng (Databases & RabbitMQ)

Để bắt đầu làm việc, bạn cần chạy hạ tầng nền tảng.

1. Tại thư mục gốc của dự án, copy file `.env.example` thành `.env` (nếu chưa có):
   ```bash
   cp .env.example .env
   ```
2. Mở Terminal và chạy lệnh sau để khởi động hạ tầng Dev:

   ```bash
   docker compose -f docker-compose.dev.yml up -d
   ```

   Lệnh này sẽ chạy các container: `grading-postgres`, `grading-sqlserver`, `grading-rabbitmq`.

   > **💡 Lưu ý cho máy Mac chip M (ARM64):**
   > Image của `sqlserver` hiện tại chỉ hỗ trợ kiến trúc `linux/amd64`. Tuy nhiên, file compose đã được cấu hình `platform: linux/amd64` nên Docker trên Mac sẽ tự động giả lập và chạy SQL Server bình thường.

## 3. Khởi chạy Backend (API & Worker)

Sau khi hạ tầng Docker đã chạy, bạn khởi động API và Worker bằng lệnh `dotnet run` (mở mỗi cái ở một Terminal riêng):

### 3.1. Chạy API

```bash
cd be/GradingSystem.Api
dotnet run
```

- API sẽ chạy mặc định tại: `http://localhost:5049`
- Tài liệu Swagger: `http://localhost:5049/swagger`

### 3.2. Chạy Worker (Xử lý chấm điểm)

```bash
cd be/GradingSystem.Worker
dotnet run
```

- Worker sẽ lắng nghe các job chấm điểm từ RabbitMQ.

## 4. Khởi chạy Frontend (Next.js)

Mở một Terminal mới để chạy Frontend:

```bash
cd fe
npm install
npm run dev
```

- Giao diện Web: `http://localhost:3000`

## 5. Quản lý Hạ tầng Dev

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
