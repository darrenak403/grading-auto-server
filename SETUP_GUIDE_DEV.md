# Hướng dẫn Cài đặt & Chạy ứng dụng cho Developer (Môi trường Dev)

Tài liệu này dành cho các lập trình viên (Developer) muốn chạy Backend ở môi trường Local để code, debug và test.

## 1. Yêu cầu hệ thống

- **Docker Desktop** (để chạy DB).
- **.NET 8 SDK** (để chạy Backend API và Worker).
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

   Lệnh này sẽ chạy các container: `postgres`, `sqlserver`, `rabbitmq`.

   > **💡 Lưu ý cho máy Mac chip M (ARM64):**
   > Image của `sqlserver` (`mcr.microsoft.com/mssql/server`) hiện tại chỉ hỗ trợ kiến trúc `linux/amd64`. Khi chạy lệnh trên máy Mac M1/M2/M3, bạn có thể thấy cảnh báo _"The requested image's platform (linux/amd64) does not match the detected host platform"_. Đừng lo lắng, Docker trên Mac (thông qua Rosetta) vẫn sẽ tự động giả lập và chạy SQL Server bình thường.

## 3. Khởi chạy Backend (.NET API & Worker)

1. Mở file `Project.sln` bằng Visual Studio hoặc Rider.
2. Thiết lập dự án `GradingSystem.Api` làm **Startup Project** và chạy (nhấn F5 hoặc nút Play).
3. (Tùy chọn) Nếu bạn đang phát triển phần xử lý background job, bạn cũng có thể mở Terminal mới, trỏ vào thư mục `GradingSystem.Worker` và chạy `dotnet run`.
4. API sẽ chạy ở địa chỉ mặc định: `http://localhost:5049` (hoặc cổng được set trong `launchSettings.json`).
5. Vào `http://localhost:5049/swagger` để xem tài liệu API.

## 4. Quản lý Hạ tầng Dev

- Nếu muốn xem **PGWeb** (Giao diện web để quản lý Database Postgres), bạn chạy lệnh kèm profile `tools`:

  ```bash
  docker compose -f docker-compose.dev.yml --profile tools up -d pgweb
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
