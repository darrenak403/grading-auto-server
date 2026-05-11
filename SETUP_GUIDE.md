# Hướng dẫn Cài đặt & Chạy ứng dụng bằng Docker

Tài liệu này hướng dẫn cách chạy hệ thống Auto Grader chỉ với Docker mà không cần tải mã nguồn về.

## 1. Các Images cần Pull

Hệ thống được đóng gói thành 3 images chính trên Docker Hub (thay `your-dockerhub-username` bằng username thực tế của bạn):

1. **API**: `ngothanhdatak/grading-system-api:latest`
2. **Worker**: `ngothanhdatak/grading-system-worker:latest`
3. **Frontend**: `ngothanhdatak/grading-system-fe:latest`

_(Ngoài ra hệ thống còn dùng thêm các images phụ trợ có sẵn: `postgres:16-alpine`, `mcr.microsoft.com/mssql/server:2022-latest`, `rabbitmq:3-management-alpine`, `sosedoff/pgweb`)_

## 2. Cách chạy lên để sử dụng ngay

Bạn không cần phải chạy lệnh `docker pull` thủ công. Chỉ cần làm 2 bước cực kỳ đơn giản sau:

### Bước 1: Chuẩn bị file

Bạn sẽ được cung cấp 2 file cấu hình đã được thiết lập sẵn:
1. File `docker-compose.prod.yml`
2. File `.env` (chứa các cấu hình mặc định an toàn)

Hãy đặt 2 file này vào chung một thư mục trên máy tính của bạn. Mở Terminal (hoặc Command Prompt) tại thư mục đó.

### Bước 2: Chạy hệ thống

Mở Terminal / Command Prompt tại thư mục chứa 2 file trên và gõ lệnh sau:

```bash
docker compose -f docker-compose.prod.yml up -d
```

Lệnh này sẽ tự động tải tất cả các images cần thiết về và khởi động ngầm (background). Quá trình tải có thể mất vài phút tùy tốc độ mạng.

## 3. Cách sử dụng

Sau khi lệnh trên chạy xong, bạn có thể truy cập ngay vào hệ thống qua trình duyệt:

- **Giao diện Web (Frontend)**: `http://localhost:3000`
- **Tài liệu API (Swagger)**: `http://localhost:5049/swagger`
- **Quản lý RabbitMQ**: `http://localhost:15672` (Dùng tài khoản đã đặt trong `.env`)
- **Quản trị DB Postgres (PGWeb)**: `http://localhost:8081`

_(Lưu ý: Nếu bạn cài đặt trên một máy chủ (Server/VPS) khác, hãy thay chữ `localhost` bằng IP của máy chủ đó)_

## 4. Các lệnh quản lý cơ bản

- **Xem log hệ thống**: `docker compose -f docker-compose.prod.yml logs -f`
- **Tạm dừng hệ thống**: `docker compose -f docker-compose.prod.yml stop`
- **Tắt và xóa hệ thống (Giữ lại dữ liệu)**: `docker compose -f docker-compose.prod.yml down`
- **Tắt và xóa SẠCH dữ liệu DB**: `docker compose -f docker-compose.prod.yml down -v`
