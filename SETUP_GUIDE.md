# Hướng Dẫn Cài Đặt Toàn Tập: PRN232 Auto Grader (Dành Cho Máy Khách)

Tài liệu này hướng dẫn cài đặt hệ thống Auto Grader lên Server hoặc máy cá nhân để sử dụng thông qua Docker.

> **💡 Lưu ý cho Developer:** Nếu bạn muốn phát triển code, hãy xem file `be/SETUP_GUIDE_DEV.md`.

---

## 🚀 Cài Đặt Siêu Tốc Bằng Docker (Production)

Phương án này phù hợp khi bạn muốn triển khai và sử dụng hệ thống ngay lập tức. Toàn bộ môi trường (API, Worker, Databases, Playwright) đã được đóng gói sẵn trong Docker.

### Lệnh Cài Đặt (Chỉ Cần Copy & Paste)

Mở Terminal ở một thư mục trống bất kỳ và chạy khối lệnh sau:

```bash
# 1. Tải file cấu hình Docker Compose từ GitHub
curl -O https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/docker-compose.prod.yml

# 2. Tải file cấu hình biến môi trường và đổi tên thành .env
curl -o .env https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/.env.example

# 3. Khởi chạy toàn bộ hệ thống
docker compose -f docker-compose.prod.yml up -d
```

### Cách Sử Dụng

Hệ thống sẽ chạy ngầm. Sau vài phút, bạn có thể truy cập ngay:

- **Giao diện Web (Frontend)**: `http://localhost:3000`
- **Quản lý RabbitMQ**: `http://localhost:15672`
- **Tài liệu API (Swagger)**: `http://localhost:5049/swagger`

---

## 🛠 CÁC LỖI THƯỜNG GẶP (Troubleshooting)

### 1. Lỗi `The value cannot be an empty string. (Parameter 'path')` khi bấm Grade

- **Nguyên nhân**: Khi một bài chấm bị lỗi (Failed), Worker sẽ tự động **xoá file artifact.zip** của bài đó để giải phóng dung lượng. Nếu bạn bấm nút "Grade Submissions" lần nữa mà không Upload lại file mới, Worker sẽ không tìm thấy file zip để chấm.
- **Cách sửa**: Chọn lại file `master.zip` trên máy tính và bấm **Upload Submissions** trước khi bấm Grade.

### 2. Cấu trúc file Zip của sinh viên

- Đảm bảo file zip của sinh viên upload lên có cấu trúc đúng (Giải nén ra phải thấy ngay file `.dll` hoặc thư mục chứa file `.csproj`). Nếu bọc trong quá nhiều thư mục con, hệ thống có thể không tìm thấy file thực thi.

### 3. Lỗi trùng lặp kết quả (Duplicate Key)

- Lỗi này đã được xử lý triệt để trong các bản cập nhật mới nhất bằng cơ chế dọn dẹp bộ nhớ đệm tự động khi quá trình chấm điểm gặp sự cố.
