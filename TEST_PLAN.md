# Kịch bản Test End-to-End (E2E Test Plan)

Dựa trên các file thực tế đã có sẵn trong thư mục `supports/` của dự án, bạn có thể tự mình test toàn bộ các flow chính của hệ thống từ A-Z. Dưới đây là hướng dẫn từng bước **đúng chuẩn 100% theo các nút bấm trên giao diện hiện tại**.

## Các file sẽ sử dụng (đã có sẵn trên máy bạn)

- **SQL mẫu:** `supports/given/database.sql`
- **API mẫu (Zip):** `supports/given/givenAPI.zip`
- **Danh sách SV:** `supports/participants.csv`
- **Bài nộp của SV:** `supports/master.zip` (Nặng 33MB)
- **Đề bài chi tiết:** `supports/101.md`

---

## 🟢 Flow 1: Khởi tạo Kỳ thi & Đề thi (Exam Session & Assignment)

1. Mở trình duyệt vào trang chủ: `http://localhost:3000/exam-sessions`
2. Bấm nút màu cam **+ New Exam Session**.
   - Tại ô **Title**: Nhập `PE PRN232 - Ky Xuan 2026`.
   - Bấm nút **Create Exam Session**.
3. Hệ thống sẽ tự chuyển sang trang chi tiết của Session. Tại tab **Assignments**, bấm nút **+ New Assignment**.
   - Tại ô **Code**: Nhập `101`.
   - Tại ô **Title**: Nhập `Ma de 101`.
   - Bấm nút **Create**.
4. Trên thẻ Đề thi vừa tạo, bấm nút màu cam **Manage** để vào trang cấu hình Đề thi chi tiết.
5. Tại tab **Setup**, bạn sẽ thấy thông báo khoá chức năng (màu đỏ) nếu chưa import danh sách.
   - Kéo xuống mục **Step 1: Import Participants**: Bấm vào dòng chữ **Chọn file .csv…**, chọn file `supports/participants.csv`.
   - Sau đó bấm nút màu cam **Import Participants** bên cạnh. Chờ thông báo chữ xanh hiện ra.
   - Kéo xuống mục **Assignment Setup**:
     - Tại dòng **Database SQL (.sql)**: Bấm **Chọn file .sql…** -> Chọn file `supports/given/database.sql`.
     - Tại dòng **Given API ZIP**: Bấm **Chọn file .zip…** -> Chọn file `supports/given/givenAPI.zip`.
     - Bấm nút màu cam **Upload Resources**. Chờ thông báo chữ xanh hiện ra.

## 🟢 Flow 2: Thiết lập Câu hỏi & Test Case

1. Chuyển sang tab **Questions**.
2. Bấm nút màu cam **+ New Question**.
   - **Title:** Nhập `Question 1`.
   - **Type:** (Mặc định chọn là API).
   - **Max Score:** Nhập `10`.
   - **Artifact Folder Name:** Nhập `Q1` (Dựa theo đề trong file 101.md, sinh viên sẽ build file ra thư mục Q1...).
   - Bấm **Create**.
3. Một thẻ câu hỏi vừa được tạo ra. Bấm nút màu cam **Manage** nằm trên thẻ đó để mở sang màn hình quản lý **Test Cases**.
4. Ở màn hình Test Cases, tạo nhanh một test case đơn giản bằng nút **+ New Test Case**:
   - **HTTP Method:** Chọn `GET`.
   - **URL Path:** Nhập `/api/students`.
   - **Expected Status Code:** Nhập `200`.
   - Các trường khác có thể để trống. Bấm **Create**.
5. Bấm nút quay lại (Back) trên trình duyệt để trở về trang quản lý Đề thi.

## 🟢 Flow 3: Nộp bài & Chấm thi tự động (Submissions & Grading)

1. Đang ở trang Đề thi, chuyển sang tab **Submit & Grade**.
2. Kéo xuống phần **Upload & Grade Submissions**:
   - Ở mục **Submissions ZIP**, bấm vào dòng chữ **Chọn master.zip…** và trỏ tới file **`supports/master.zip`**.
   - Bấm nút màu cam **Upload Submissions**. Việc này có thể mất vài giây vì file nặng 33MB. Chờ chữ xanh báo `Created: 3, Parsed: 3` hiện lên.
3. Nhìn xuống mục dưới cùng **Grade Submissions**:
   - Kiểm tra ô **Grading Round** đã điền chữ `Lần 1` chưa (nếu chưa thì gõ vào).
   - Bấm nút màu xanh lá **▶ Grade Submissions**.
4. Hệ thống sẽ đẩy 3 job chấm bài cho 3 sinh viên vào RabbitMQ. Bạn kéo lên trên xem danh sách Submissions, sẽ thấy status tự động nhảy từ _Pending_ -> _Running_ -> _Done_.
5. **(Tùy chọn)**: Lúc bấm Grade, hãy mở cửa sổ Terminal đang chạy `dotnet run` của Worker lên để tận mắt xem log hệ thống đang bung code và chạy lệnh chấm điểm tự động.

## 🟢 Flow 4: Xem điểm & Chấm tay (Review & Adjust)

1. Khi các bài đã chấm xong (Status báo _Done_), cột **Score** trong bảng danh sách Submissions sẽ hiển thị điểm tự động (`.../10`).
2. Bấm nút màu cam **View** ở dòng của sinh viên bất kỳ.
3. Trang chi tiết bài làm hiện ra:
   - Bên trái sẽ hiển thị kết quả của Test Case `/api/students` (Pass hay Fail, và log Response Body thực tế máy chủ nhận được từ code của sinh viên).
   - Bên phải là phần **Manual Grading**. Bạn có thể gõ vào ô **Adjusted Score** (VD: 8) và nhập lý do (VD: Code lỗi format tí thôi) ở ô **Review Note**. Bấm Save là điểm ghi đè sẽ được áp dụng.

## 🟢 Flow 5: Xuất báo cáo (Export Excel)

1. Bấm nút quay lại (Back) trở về trang Đề thi, chuyển sang tab **Results & Export**.
2. Ở mục **Export Assignment Results**, ô Grading Round giữ nguyên `Lần 1`. Bấm nút màu cam **Create Export**.
3. Chờ trạng thái Export nhảy thành màu xanh lá (Done). Nút **Download** màu cam sẽ xuất hiện.
4. Bấm Download, một file Excel (`.xlsx`) sẽ được tải về. Mở ra để xem bảng điểm tổng hợp cực kỳ chuyên nghiệp.

---

_Chúc bạn test thành công! Bản hướng dẫn này đã khớp 100% với từng câu chữ và nút bấm trên giao diện ứng dụng của bạn._
