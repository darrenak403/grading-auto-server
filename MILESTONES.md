Hệ thống PRN232 Auto Grader được chia làm **5 Milestone (Luồng) chính**. Về phía Frontend (FE - Next.js), ứng dụng sử dụng kiến trúc App Router (`src/app`), giao tiếp với Backend qua các hàm gọi API RESTful (đặt trong thư mục `src/lib/api`).

Dưới đây là chi tiết từng Milestone và cách Frontend xử lý chúng:

### Milestone 1: Khởi tạo dữ liệu (Quản lý Đề thi)

_Mục đích: Giảng viên tạo đề thi, upload các database mẫu, thiết lập câu hỏi và test case._

- **FE Flow chạy như thế nào:**
  - **URL:** `/assignments` và `/assignments/[id]` (Tab: Setup & Questions).
  - **Tạo Assignment:** FE hiển thị form cho phép nhập tên và mã đề. Khi submit, gọi `POST /assignments`.
  - **Tab Setup:** FE dùng thẻ `<input type="file">` để đọc file `database.sql` và `given.zip`. Dữ liệu được gói vào `FormData` và bắn lên API `PUT /assignments/{id}/resources`.
  - **Tab Questions:** FE render danh sách các câu hỏi. Khi giảng viên bấm "New Question", FE gọi API tạo câu hỏi. Bấm vào từng câu hỏi sẽ nhảy sang trang Quản lý Test Cases (để thêm input/expected output cho hệ thống tự động dò).

### Milestone 2: Tổ chức kỳ thi (Exam Sessions)

_Mục đích: Gom nhóm nhiều đề thi lại vào chung 1 đợt thi (ví dụ: Thi Final kỳ Spring 2024)._

- **FE Flow chạy như thế nào:**
  - **URL:** `/exam-sessions` và `/exam-sessions/[id]`.
  - **Tạo Session:** Gọi `POST /exam-sessions`.
  - **Gán Đề thi (Assign):** FE cho phép giảng viên chọn các Assignment đã tạo ở Milestone 1 và gắn vào Session này. Trang chi tiết Session sẽ fetch toàn bộ kết quả của các Assignment con để làm bảng tổng hợp điểm.

### Milestone 3: Nộp bài & Chấm thi tự động

_Mục đích: Đưa source code của toàn bộ sinh viên vào hệ thống và kích hoạt Worker chấm bài. Hỗ trợ nhiều lần chấm (grading rounds)._

- **FE Flow chạy như thế nào:**
  - **URL:** `/assignments/[id]` (Tab: Submit & Grade).
  - **Import Sinh viên:** FE cho upload file `.csv` chứa mã số SV, gọi API `POST .../participants/import`.
  - **Quản lý Rounds:** FE hiển thị dropdown để chọn round hiện tại ("Lần 1", "Lần 2", ...). Mỗi round độc lập. FE gọi `GET .../rounds` để lấy danh sách và `POST .../rounds` để tạo round mới (auto-numbered).
  - **Upload Bài thi:** Giảng viên có 2 cách: (1) upload một file `.zip` bất kỳ tên vào round hiện tại qua `POST .../bulk-upload`, hoặc (2) tạo round mới (tự động đánh số "Lần N+1") qua `POST .../rounds` với file upload.
  - **Trigger Grading:** Khi giảng viên bấm nút "Grade", FE gọi `POST .../grade`. FE polling `GET /grading-jobs` mỗi 2 giây để cập nhật thanh tiến trình từ `Pending` -> `Running` -> `Done`.
  - **Retry Failed:** Nếu một submission fail, giảng viên bấm nút "Retry" (chỉ hiển thị khi latestJobStatus=Failed) để gọi `POST /submissions/{id}/grade`. Endpoint này chỉ cho phép retry khi job cuối cùng fail (infra recovery only).

### Milestone 4: Xem kết quả & Điều chỉnh điểm (Chấm tay)

_Mục đích: Xem máy chấm như thế nào và can thiệp ghi đè điểm nếu máy chấm sai._

- **FE Flow chạy như thế nào:**
  - **URL:** `/submissions/[id]` hoặc `/grading`
  - **Hiển thị:** FE gọi API lấy `Submission` chi tiết. Màn hình chia làm 2 phần: Bên trái là danh sách các Test Case (có badge xanh/đỏ cho biết Pass hay Fail kèm response máy chủ trả về).
  - **Chỉnh điểm (Adjust Score):** Có một form nhỏ cho phép Giảng viên nhập `AdjustedScore` và `ReviewNote`. Khi gõ xong, FE gọi `PUT /question-results/{id}/adjust`. Điểm sau khi chỉnh sửa sẽ ngay lập tức đè lên điểm tự động của máy (`AutoScore`).

### Milestone 5: Xuất báo cáo (Excel Export)

_Mục đích: Xuất bảng điểm cuối cùng ra file Excel nộp cho nhà trường._

- **FE Flow chạy như thế nào:**
  - **URL:** `/assignments/[id]` (Tab: Results & Export) hoặc `/exam-sessions/[id]`.
  - **Tạo Job:** Giảng viên bấm nút "Xuất Excel", FE gọi `POST /exports`. Vì việc tạo Excel mất thời gian, Backend không trả về file ngay mà trả về một `ExportJob ID`.
  - **Polling & Download:** Tương tự lúc chấm thi, FE dùng `setInterval` để theo dõi trạng thái Job này. Khi trạng thái biến thành `Done`, FE tự động tạo một thẻ thẻ ảo (`document.createElement('a')`), gắn URL tải file (`GET /exports/{id}/download`) vào và mô phỏng cú click chuột để trình duyệt tự động bắt file `.xlsx` tải xuống máy tính cho Giảng viên.

Toàn bộ luồng FE được thiết kế theo hướng **Single Page Application (SPA)** cực kỳ mượt mà, sử dụng Next.js App Router và quản lý state khá tiêu chuẩn bằng React Hooks (`useState`, `useEffect`).
