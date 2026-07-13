# Grading Sync Guide v2

> Ngày cập nhật: 2026-07-05
>
> Tài liệu này là bản rút gọn, thực dụng để sửa lỗi "sync không lên Supabase" sau khi hệ thống đã chuyển sang schema ERD mới.

## 1. Kết luận nhanh

Nếu grading tool vẫn gửi payload kiểu cũ như:

```json
{
  "termId": "e333d25d-ced2-402f-8150-6e4e358b3282",
  "labId": "LAB1",
  "className": "SE1812"
}
```

thì **không đủ dữ liệu để tạo submission** trong schema hiện tại.

Lý do:

- App hiện tại **không có route/server action** nào nhận payload chỉ gồm `termId + labId + className` để sync điểm.
- Nếu muốn sync toàn bộ một session chấm, gọi `POST /api/v1/lab-assignments/sync-supabase-grades` với `submissions[]`.
- Schema mới **không ghi trực tiếp** vào bảng `submissions` cũ.
- Schema mới yêu cầu resolve ra:
  - `class_student_id`
  - `class_lab_id`
- Sau đó phải gọi RPC:
  - `create_class_lab_submission`

## 2. Schema mới đang dùng gì

Frontend/admin hiện tại đọc dữ liệu từ các bảng mới:

- `terms`
- `classes`
- `students`
- `class_students`
- `labs`
- `class_labs`
- `class_lab_submissions`
- `resubmission_requests_v2`

Điểm số hiển thị trong app đến từ `class_lab_submissions`, không phải `submissions`.

## 3. Vì sao payload cũ fail

Payload cũ chỉ cho biết:

- lớp nào: `className`
- lab nào: `labId`
- học kỳ nào: `termId`

Nhưng để tạo một dòng chấm bài, hệ thống còn cần biết:

- sinh viên nào: phải resolve ra `class_student_id`
- lab của lớp nào: phải resolve ra `class_lab_id`
- loại lần nộp: `original` / `late` / `resubmit`
- điểm số thực tế: `score`
- chi tiết chấm: `details`

Nói ngắn gọn: payload cũ mới mô tả "context", chưa mô tả "submission".

## 4. Contract đúng của flow mới

### API-level payload cho một session chấm

```http
POST /api/v1/lab-assignments/sync-supabase-grades
```

```json
{
  "termId": "e333d25d-ced2-402f-8150-6e4e358b3282",
  "className": "SE1812",
  "labCode": "LAB1",
  "submissions": [
    {
      "studentCode": "SE161039",
      "score": 8.5,
      "details": {
        "passed": 17,
        "failed": 3,
        "tests": []
      },
      "sourceUrl": "https://github.com/example/repo"
    }
  ]
}
```

Quy tắc:

- `termId` optional về mặt API, nhưng nên gửi từ dropdown term để tránh resolve nhầm khi class/lab trùng giữa nhiều term.
- `className` và `labCode` là context chung của session chấm.
- `submissions[]` chứa toàn bộ sinh viên trong session cần sync.
- Backend xử lý từng submission độc lập: item lỗi sẽ nằm trong `failed[]`, item còn lại vẫn tiếp tục sync.

Response `data`:

```json
{
  "total": 1,
  "syncedCount": 1,
  "failedCount": 0,
  "synced": [
    {
      "studentCode": "SE161039",
      "classStudentId": "uuid",
      "classLabId": "uuid",
      "itemType": "original",
      "fulfillsRequestId": null
    }
  ],
  "failed": []
}
```

Các bước dưới đây là logic Supabase nội bộ mà API thực hiện cho từng item trong `submissions[]`.

### Bước 1: Normalize input

Tool nên chuẩn hóa:

- `student_code = student_code.trim().toUpperCase()`
- `class_name = class_name.trim().toUpperCase()`
- `lab_code = lab_code.trim().toUpperCase()`

### Bước 2: Resolve `class_student_id`

```http
GET /rest/v1/class_students?select=id,students!inner(student_code),classes!inner(name)&students.student_code=eq.{STUDENT_CODE}&classes.name=eq.{CLASS_NAME}
```

Nếu trả về `[]`:

- sinh viên chưa có trong roster của lớp
- hoặc `student_code` / `class_name` đang sai format

### Bước 3: Resolve `class_lab_id`

```http
GET /rest/v1/class_labs?select=id,deadline,labs!inner(code),classes!inner(name)&labs.code=eq.{LAB_CODE}&classes.name=eq.{CLASS_NAME}
```

Nếu trả về `[]`:

- lab chưa được assign cho lớp
- hoặc `lab_code` / `class_name` không khớp dữ liệu thực

### Bước 4: Kiểm tra resubmission đang `approved`

```http
GET /rest/v1/resubmission_requests_v2?select=id&class_student_id=eq.{CLASS_STUDENT_ID}&class_lab_id=eq.{CLASS_LAB_ID}&status=eq.approved&created_submission_id=is.null&order=updated_at.desc&limit=1
```

### Bước 5: Gọi RPC tạo submission

```http
POST /rest/v1/rpc/create_class_lab_submission
```

Payload đúng:

```json
{
  "p_class_student_id": "uuid",
  "p_class_lab_id": "uuid",
  "p_item_type": "original",
  "p_source_url": "https://github.com/...",
  "p_score": 8.5,
  "p_details": {
    "passed": 17,
    "failed": 3
  },
  "p_fulfills_request_id": null
}
```

## 5. Quy tắc `p_item_type`

- Nếu có request `approved`: dùng `resubmit`
- Nếu không có request:
  - còn trong hạn: `original`
  - quá hạn: `late`

Không dùng `termId` để xác định `item_type`.

## 6. Headers bắt buộc

Các call REST/RPC phía grading tool phải dùng `service role key`:

```http
apikey: {SUPABASE_SERVICE_ROLE_KEY}
Authorization: Bearer {SUPABASE_SERVICE_ROLE_KEY}
Content-Type: application/json
```

Nếu dùng anon key hoặc JWT client-side sai ngữ cảnh, request có thể fail vì quyền.

## 7. Checklist debug nhanh

Khi sync không lên, kiểm tra theo thứ tự này:

1. Có tìm thấy `class_student_id` không?
2. Có tìm thấy `class_lab_id` không?
3. `LAB1` và `SE1812` đã được normalize uppercase chưa?
4. Lab đã được assign cho lớp đó chưa?
5. Request có đang gọi `create_class_lab_submission` không?
6. Request có đang ghi nhầm vào bảng `submissions` cũ không?
7. Request có dùng `SUPABASE_SERVICE_ROLE_KEY` không?

## 8. SQL để test nhanh trên Supabase

Kiểm tra lớp:

```sql
select id, name
from classes
where name = 'SE1812';
```

Kiểm tra lab đã assign cho lớp:

```sql
select cl.id, c.name as class_name, l.code as lab_code, cl.deadline
from class_labs cl
join classes c on c.id = cl.class_id
join labs l on l.id = cl.lab_id
where c.name = 'SE1812'
  and l.code = 'LAB1';
```

Kiểm tra roster sinh viên của lớp:

```sql
select cs.id, s.student_code, c.name
from class_students cs
join students s on s.id = cs.student_id
join classes c on c.id = cs.class_id
where c.name = 'SE1812';
```

Kiểm tra submission mới nhất:

```sql
select *
from class_lab_submissions
order by submitted_at desc
limit 20;
```

## 9. Mẫu pseudo-flow đúng

```text
input: student_code, class_name, lab_code, score, details, source_url

normalize upper-case
resolve class_student_id
resolve class_lab_id + deadline
find approved resubmission request
decide item_type
call rpc create_class_lab_submission
if fulfilled approved request -> patch request to completed
```

## 10. Kết luận kỹ thuật

Lỗi hiện tại không nằm ở `lib/actions/erd-admin.ts`.

Lỗi nằm ở chỗ grading tool hoặc backend sync bên ngoài vẫn đang dùng contract cũ. Nếu muốn dữ liệu hiện trên app này, tool phải chuyển sang flow RPC của schema mới.
