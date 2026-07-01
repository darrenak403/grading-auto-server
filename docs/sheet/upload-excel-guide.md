# Hướng dẫn Upload Bảng Điểm Excel lên Google Sheets & Tích hợp API

Tài liệu này hướng dẫn cách đưa dữ liệu từ các file Excel bảng điểm chấm tự động (ví dụ: `SE1815.xlsx`) lên Google Sheets làm database và cách gọi các API Google Apps Script tương ứng để các công cụ khác hoặc Next.js có thể tích hợp.

---

## 1. Chuẩn Bị & Định Dạng File Excel

File Excel kết quả chấm điểm (ví dụ: `SE1815.xlsx`) cần đáp ứng các điều kiện sau:
* **Có dòng tiêu đề (Headers)** ở hàng đầu tiên (Hàng 1).
* **Bắt buộc có cột đặt tên là `MSSV`** (Mã số sinh viên) để làm khóa ngoại liên kết với tài khoản đăng nhập.
* Các cột tiêu chí chấm điểm chi tiết (ví dụ: *Solution has 3 .csproj files*, *docker-compose.yml present*...) và cột tổng điểm (ví dụ: `Grand Total` hoặc `Score`).
* Không chứa các dòng trống xen kẽ ở giữa danh sách sinh viên.

---

## 2. Các Bước Upload Lên Google Sheets

Để upload file Excel của lớp học vào hệ thống:
1. Mở file Google Sheet đang làm Database của bạn.
2. Chọn **Tệp (File)** -> **Nhập (Import)**.
3. Chọn tab **Tải lên (Upload)** -> Kéo thả hoặc chọn file Excel của bạn (ví dụ: `SE1815.xlsx`) từ máy tính.
4. Tại mục cấu hình nhập:
   * **Vị trí nhập (Import location)**: Chọn **Chèn (các) trang tính mới (Insert new sheet(s))**.
   * Bấm **Nhập dữ liệu (Import data)**.
5. Google Sheets sẽ tạo ra một tab trang tính mới. 
6. **Đổi tên trang tính (Sheet Name)** này thành tên lớp viết liền (Ví dụ: `SE1815`). Đây chính là tham số tên lớp (`class`) sẽ truyền vào API.

---

## 3. Cấu Hình Google Apps Script

Mã nguồn Apps Script nằm trong file `docs/api/google-apps-script.js`.
1. Copy toàn bộ code trong file đó.
2. Mở Google Sheet -> Chọn **Tiện ích mở rộng (Extensions)** -> **Apps Script**.
3. Dán đè mã nguồn vào file `Code.gs`.
4. Thay thế `SPREADSHEET_ID` bằng ID sheet của bạn.
5. Bấm **Triển khai (Deploy)** -> **Triển khai mới (New deployment)** (chọn loại *Ứng dụng Web / Web app*, thực thi dưới quyền *Tôi / Me*, ai có quyền truy cập *Bất kỳ ai / Anyone*).
6. Copy URL Web App nhận được (dạng `https://script.google.com/macros/s/XXXXX/exec`).

---

## 4. Tài Liệu Tích Hợp API (API Contract)

API hoạt động thông qua URL Web App của Google Apps Script. 
> **Lưu ý**: Đối với phương thức `POST`, các công cụ tích hợp hoặc Next.js nên gửi Request với header `Content-Type: text/plain` (nhưng nội dung body là chuỗi JSON stringified) để tránh lỗi CORS preflight (OPTIONS) của trình duyệt.

### A. Kiểm tra email & lấy MSSV (GET)
Dùng để xác thực sinh viên đăng nhập có nằm trong danh sách lớp không và lấy MSSV tương ứng.
* **URL**: `https://script.google.com/macros/s/XXXXX/exec?action=checkEmail&email={student_email}`
* **Method**: `GET`
* **Response (JSON)**:
  ```json
  {
    "success": true,
    "allowed": true,
    "studentId": "SE182672"
  }
  ```

### B. Lấy điểm của riêng Sinh viên trong bảng điểm lớp (GET)
Dùng để sinh viên tự tra cứu điểm số chi tiết của lớp học mà không xem được điểm của sinh viên khác.
* **URL**: `https://script.google.com/macros/s/XXXXX/exec?action=getStudentClassGrades&email={student_email}&class={class_name}`
* **Method**: `GET`
* **Response (JSON)**:
  ```json
  {
    "success": true,
    "found": true,
    "data": {
      "name": "SE182672",
      "MSSV": "SE182672",
      "Solution has 3 .csproj files (3-layer architecture)": "1.00/1.00",
      "docker-compose.yml present in submission": "1.00/1.00",
      "Grand Total": "12.50/12.50",
      "Status": "Done"
    }
  }
  ```

### C. Lấy toàn bộ điểm của cả lớp (GET - Dành cho Giảng viên / Admin)
Dùng để hiển thị bảng điểm của cả lớp cho giảng viên.
* **URL**: `https://script.google.com/macros/s/XXXXX/exec?action=getClassGrades&class={class_name}`
* **Method**: `GET`
* **Response (JSON)**:
  ```json
  {
    "success": true,
    "data": [
      {
        "name": "SE150361",
        "MSSV": "SE150361",
        "Grand Total": "12.50/12.50",
        "Status": "Done"
      },
      {
        "name": "SE151283",
        "MSSV": "SE151283",
        "Grand Total": "4.50/12.50",
        "Status": "Done"
      }
    ]
  }
  ```

### D. Đồng bộ thông tin Sinh viên khi đăng nhập (POST)
Ghi nhận thông tin sinh viên vào sheet `Students`.
* **URL**: `https://script.google.com/macros/s/XXXXX/exec`
* **Method**: `POST`
* **Payload (JSON String)**:
  ```json
  {
    "action": "syncUser",
    "data": {
      "email": "student@fpt.edu.vn",
      "name": "Nguyen Van A",
      "student_id": "SE182672",
      "class_name": "SE1815"
    }
  }
  ```

### E. Lưu kết quả chấm điểm Lab tự động (POST)
Dùng để các công cụ Auto-Grader (máy chấm bài) đẩy điểm nộp bài của sinh viên trực tiếp vào sheet `Submissions`.
* **URL**: `https://script.google.com/macros/s/XXXXX/exec`
* **Method**: `POST`
* **Payload (JSON String)**:
  ```json
  {
    "action": "saveSubmission",
    "data": {
      "email": "student@fpt.edu.vn",
      "lab_id": "Lab1_Csharp_Basics",
      "score": 8.5,
      "status": "PASSED",
      "details": {
        "passed": 8,
        "failed": 2,
        "errors": ["Test 9 failed", "Test 10 failed"]
      }
    }
  }
  ```
