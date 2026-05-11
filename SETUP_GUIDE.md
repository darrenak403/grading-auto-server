# Hướng Dẫn Cài Đặt Toàn Tập: PRN232 Auto Grader

Tài liệu này bao gồm 2 phương án cài đặt: **Phương án 1** dành cho Server/Production (chỉ dùng Docker) và **Phương án 2** dành cho Developer (chạy code trực tiếp trên máy cá nhân).

---

## 🚀 PHƯƠNG ÁN 1: Cài Đặt Siêu Tốc Bằng Docker (Production)

Phương án này phù hợp khi bạn muốn triển khai lên Server hoặc chạy thử nhanh mà không cần tải source code về.
Toàn bộ hệ thống (kể cả môi trường Playwright) đã được đóng gói sẵn trong Docker.

### Lệnh Cài Đặt Tự Động (Chỉ Cần Copy & Paste)

Mở Terminal ở một thư mục trống bất kỳ và chạy khối lệnh sau:

```bash
# 1. Tải file cấu hình Docker Compose từ GitHub
curl -O https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/docker-compose.prod.yml

# 2. Tải file cấu hình biến môi trường và đổi tên thành .env
curl -o .env https://raw.githubusercontent.com/darrenak403/grading-auto-server/main/.env.example

# 3. Khởi chạy toàn bộ hệ thống
docker compose -f docker-compose.prod.yml up -d
```

Hệ thống sẽ chạy ngầm. Quá trình tải có thể mất vài phút. Truy cập ngay:

- **Giao diện Web (Frontend)**: `http://localhost:3000`
- **Quản lý RabbitMQ**: `http://localhost:15672`
- **Tài liệu API (Swagger)**: `http://localhost:5049/swagger`

---

## 💻 PHƯƠNG ÁN 2: Cài Đặt Local Development (Dành Cho Coder)

Phương án này dùng để phát triển tính năng mới. Bạn sẽ chạy Database bằng Docker, nhưng chạy Backend và Frontend trực tiếp trên máy Mac / Windows.

**Yêu cầu:** Đã cài sẵn `.NET 8 SDK`, `Node.js`, và `Docker Desktop`.

### Bước 1: Chạy Cơ Sở Hạ Tầng (Database & RabbitMQ)

Chạy file `docker-compose.dev.yml` (hoặc file tương tự chứa cấu hình DB của bạn) để khởi động Postgres, SQL Server và RabbitMQ:

```bash
docker compose -f docker-compose.dev.yml up -d
```

### Bước 2: Cài đặt Playwright (Cực Kỳ Quan Trọng)

Worker sử dụng thư viện **Playwright** để giả lập trình duyệt chấm điểm UI. Nếu không cài đặt, Worker sẽ văng lỗi `Executable doesn't exist`.
Mở Terminal, đi vào thư mục của Worker và chạy lệnh cài đặt:

```bash
cd be/GradingSystem.Worker
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

> **⚠️ Lưu ý cho Mac/Linux:** Nếu Terminal báo lỗi `command not found: playwright` (do thiếu biến môi trường PATH), hãy thay lệnh cuối bằng đường dẫn tuyệt đối sau:
> `~/.dotnet/tools/playwright install chromium`

### Bước 3: Khởi Động Backend

Mở 2 cửa sổ Terminal (hoặc split screen) để chạy song song API và Worker:

**Terminal 1 (Chạy API):**

```bash
cd be/GradingSystem.Api
dotnet run
```

**Terminal 2 (Chạy Worker):**

```bash
cd be/GradingSystem.Worker
dotnet run
```

### Bước 4: Khởi Động Frontend

Mở Terminal thứ 3:

```bash
cd fe
npm install
npm run dev
```

---

## 🛠 CÁC LỖI THƯỜNG GẶP (Troubleshooting)

### 1. Lỗi `The value cannot be an empty string. (Parameter 'path')` khi bấm Grade

- **Nguyên nhân**: Khi một bài chấm bị lỗi (Failed), Worker sẽ tự động **xoá file artifact.zip** của bài đó để giải phóng dung lượng. Nếu bạn bấm nút "Grade Submissions" lần nữa mà không Upload lại file mới, Worker sẽ không tìm thấy file zip để chấm.
- **Cách sửa**: Chọn lại file `master.zip` trên máy tính và bấm **Upload Submissions** trước khi bấm Grade.

### 2. Lỗi `Student app exited with code 1` (Đã được vá lõi)

- **Cơ chế hoạt động mới**: Worker hiện tại rất thông minh. Nếu bạn nén file **đã Publish** (chứa `.dll`), nó sẽ chạy ngay lập tức. Nếu sinh viên lười chỉ nén **Mã nguồn gốc** (chứa file `.csproj`), Worker sẽ tự động gọi lệnh `dotnet run` để tự biên dịch và chạy.
- **Lưu ý**: Đảm bảo file zip bạn upload có cấu trúc đúng (Giải nén ra phải thấy ngay file `.dll` hoặc `.csproj` chứ không được lồng trong nhiều thư mục rác).

### 3. Lỗi `duplicate key value violates unique constraint "IX_QuestionResults_..."`

- Lỗi này xuất hiện ở các hệ thống cũ khi TestRunner bị lỗi khiến dữ liệu bị lưu đè 2 lần. Code hiện tại đã được vá bằng hàm `ClearChanges()` để dọn sạch bộ nhớ đệm trước khi lưu lỗi. Đảm bảo bạn đang pull code mới nhất.
