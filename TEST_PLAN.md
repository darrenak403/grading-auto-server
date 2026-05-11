# Kịch bản Test End-to-End (E2E Test Plan)

Dựa trên các file thực tế đã có sẵn trong thư mục `supports/` của dự án, kịch bản dưới đây hướng dẫn chi tiết từng công việc cụ thể của các luồng (flow) nghiệp vụ. Sau mỗi flow, danh sách các API tương ứng được gọi dưới nền (từ Frontend xuống Backend) cũng được liệt kê rõ ràng.

## 📂 Các file tài nguyên dùng để test (trong thư mục `supports/`)

- **SQL mẫu:** `supports/given/database.sql`
- **API mẫu (Zip):** `supports/given/givenAPI.zip`
- **Danh sách SV:** `supports/participants.csv`
- **Bài nộp của SV:** `supports/master.zip` (Nặng ~33MB)
- **Đề bài chi tiết:** `supports/101.md`

---

## 🟢 Flow 1: Khởi tạo Kỳ thi & Đề thi (Exam Session & Assignment)

### Chi tiết công việc:
1. **Tạo Kỳ thi (Exam Session)**:
   - Mở trình duyệt vào trang: `http://localhost:3000/exam-sessions`.
   - Bấm nút **+ New Exam Session**.
   - Nhập **Title**: `PE PRN232 - Ky Xuan 2026` và bấm **Create Exam Session**.
2. **Tạo Đề thi (Assignment)**:
   - Trong trang chi tiết của Session vừa tạo, chuyển sang tab **Assignments**.
   - Bấm nút **+ New Assignment**.
   - Nhập **Code**: `101`, **Title**: `Ma de 101` và bấm **Create**.
3. **Cấu hình Đề thi (Import sinh viên & Upload tài nguyên)**:
   - Bấm nút **Manage** trên thẻ Đề thi vừa tạo để vào trang chi tiết Đề thi.
   - Tại tab **Setup**, kéo xuống mục **Step 1: Import Participants**.
   - Bấm **Chọn file .csv…** -> Chọn file `supports/participants.csv`. Bấm **Import Participants**.
   - Kéo xuống mục **Assignment Setup**:
     - Dòng **Database SQL (.sql)**: Chọn file `supports/given/database.sql`.
     - Dòng **Given API ZIP**: Chọn file `supports/given/givenAPI.zip`.
     - Bấm **Upload Resources** và chờ thông báo thành công.

### 🔗 API liên quan:
- `POST /api/v1/exam-sessions` — Tạo kỳ thi mới.
- `POST /api/v1/assignments` — Tạo đề thi mới (gắn vào kỳ thi).
- `POST /api/v1/assignments/{id}/participants/import` — Parse file CSV và lưu danh sách sinh viên tham gia thi.
- `PUT /api/v1/assignments/{id}/resources` — Upload các file tài nguyên cấu trúc đề thi (database SQL, mã nguồn mẫu givenAPI.zip).

---

## 🟢 Flow 2: Thiết lập Câu hỏi & Test Case

### Chi tiết công việc:
1. **Tạo Câu hỏi (Question)**:
   - Tại trang chi tiết Đề thi, chuyển sang tab **Questions**.
   - Bấm **+ New Question**.
   - Nhập **Title**: `Question 1`, **Type**: `API`, **Max Score**: `10`, **Artifact Folder Name**: `Q1` (thư mục gốc chứa source code của sv). Bấm **Create**.
2. **Tạo Test Case**:
   - Bấm nút **Manage** trên thẻ Câu hỏi vừa tạo để mở trang quản lý Test Cases.
   - Bấm nút **+ New Test Case**.
   - Chọn **HTTP Method**: `GET`, nhập **URL Path**: `/api/students`, nhập **Expected Status Code**: `200`. Bấm **Create**.
3. Bấm **Back** để trở lại trang quản lý Đề thi.

### 🔗 API liên quan:
- `GET /api/v1/assignments/{id}/questions` — Lấy danh sách câu hỏi của đề thi để hiển thị.
- `POST /api/v1/assignments/{id}/questions` — Tạo câu hỏi mới cho đề thi.
- `GET /api/v1/questions/{id}/test-cases` — Lấy danh sách các test cases của câu hỏi.
- `POST /api/v1/questions/{id}/test-cases` — Tạo một test case mới (cấu hình HTTP method, url path, body, status code mong đợi).

---

## 🟢 Flow 3: Nộp bài & Chấm thi tự động (Submissions & Grading)

### Chi tiết công việc:
1. **Upload bài nộp của Sinh viên**:
   - Đang ở trang Đề thi, chuyển sang tab **Submit & Grade**.
   - Kéo xuống phần **Upload & Grade Submissions**.
   - Tại mục **Submissions ZIP**, chọn file `supports/master.zip`.
   - Bấm **Upload Submissions** (sẽ mất vài giây vì file nặng).
2. **Kích hoạt chấm bài**:
   - Nhìn xuống mục **Grade Submissions**, nhập `Lần 1` vào ô **Grading Round**.
   - Bấm nút màu xanh lá **▶ Grade Submissions**.
3. **Theo dõi tiến độ chấm**:
   - Hệ thống sẽ đẩy Job chấm bài vào RabbitMQ. Tại danh sách Submissions phía trên, bạn sẽ thấy status tự động cập nhật từ _Pending_ -> _Running_ -> _Done_.

### 🔗 API liên quan:
- `POST /api/v1/assignments/{id}/bulk-upload` — Nhận file `master.zip` (~33MB), tự động giải nén và đối chiếu mã số sinh viên, sau đó tạo bản ghi Submission.
- `POST /api/v1/assignments/{id}/grade` — Trigger chấm thi, tạo các `GradingJob` và Publish event (message) vào RabbitMQ để Worker bắt đầu xử lý chạy Docker/Playwright.
- `GET /api/v1/assignments/{id}/submissions` — Fetch danh sách bài nộp để hiển thị tiến độ.
- `GET /api/v1/submissions/{id}/grading-jobs` — Fetch trạng thái của job chấm thi (dùng để UI có thể tự động polling cập nhật status).

---

## 🟢 Flow 4: Xem điểm & Chấm tay (Review & Adjust)

### Chi tiết công việc:
1. **Xem chi tiết kết quả**:
   - Khi bài thi chấm xong, cột **Score** sẽ hiển thị điểm cụ thể.
   - Bấm nút màu cam **View** trên dòng của sinh viên bất kỳ.
   - Cửa sổ bên trái sẽ hiển thị danh sách các Test Case đã chạy (màu xanh Pass hoặc đỏ Fail) kèm theo toàn bộ raw Response Body/Header trả về từ API của sinh viên.
2. **Chấm tay / Sửa điểm (Adjust)**:
   - Tại cửa sổ bên phải (Manual Grading), nhập điểm sửa đổi vào ô **Adjusted Score** (VD: `8`).
   - Nhập lý do vào ô **Review Note** (VD: "Code chạy ra đúng data nhưng sai format JSON").
   - Bấm **Save** để lưu lại điểm ghi đè.

### 🔗 API liên quan:
- `GET /api/v1/submissions/{id}` — Lấy chi tiết bài nộp và điểm Auto Score/Final Score.
- `GET /api/v1/submissions/{id}/results` — Lấy kết quả chi tiết từng câu hỏi (QuestionResults) cùng log của từng test case.
- `PUT /api/v1/question-results/{id}/adjust` — Lưu điểm chỉnh sửa thủ công (Adjusted Score) cho kết quả của một câu hỏi.
- `PUT /api/v1/submissions/{id}/notes` — Lưu thông tin ghi chú/nhận xét (Review Note) cho toàn bộ bài nộp.

---

## 🟢 Flow 5: Xuất báo cáo (Export Excel)

### Chi tiết công việc:
1. **Tạo yêu cầu xuất báo cáo**:
   - Trở lại trang Đề thi, chuyển sang tab **Results & Export**.
   - Ở mục **Export Assignment Results**, ô Grading Round giữ nguyên `Lần 1`. Bấm **Create Export**.
2. **Tải file Excel**:
   - Hệ thống sẽ hiển thị trạng thái Exporting. Quá trình này được xử lý bất đồng bộ, hãy chờ một lát để trạng thái đổi sang màu xanh (Done).
   - Nút **Download** xuất hiện. Bấm vào để tải file Excel (`.xlsx`) chứa bảng điểm tổng hợp của tất cả sinh viên.

### 🔗 API liên quan:
- `POST /api/v1/exports` (hoặc `POST /api/v1/exam-sessions/{id}/exports`) — Tạo job yêu cầu hệ thống tính toán và build file Excel. Job được đẩy vào background.
- `GET /api/v1/exports/{id}` — Kiểm tra trạng thái job tạo file (Pending/Processing/Done).
- `GET /api/v1/exports/{id}/download` — Endpoint tải stream file `.xlsx` về máy browser sau khi job Done.
