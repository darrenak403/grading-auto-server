# FE Handoff: Truy vấn kết quả điểm số và chi tiết chấm Lab trực tiếp từ Supabase SDK

> Branch: `tvu`
> Date: 2026-07-01

Tài liệu này hướng dẫn Frontend cách cài đặt SDK và thực hiện truy vấn trực tiếp bảng `submissions` trên Supabase để hiển thị kết quả chấm bài của sinh viên.

---

## 1) Supabase SDK Setup & Client Initialization

Frontend cần cài đặt thư viện `@supabase/supabase-js` để giao tiếp trực tiếp với Supabase.

### Cài đặt:
```bash
npm install @supabase/supabase-js
```

### Khởi tạo Client:
```typescript
// lib/supabase.ts
import { createClient } from '@supabase/supabase-js';

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL || '';
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY || '';

export const supabase = createClient(supabaseUrl, supabaseAnonKey);
```

---

## 2) Query Contract

### Thực hiện truy vấn kết quả chấm của sinh viên:

```typescript
import { supabase } from '@/lib/supabase';

// studentId: MSSV của sinh viên đang đăng nhập (ví dụ: "SE180234")
const { data, error } = await supabase
  .from('submissions')
  .select('student_id, lab_id, class_name, score, status, details, updated_at')
  .eq('student_id', studentId)
  .order('updated_at', { ascending: false });
```

### Kiểu dữ liệu trả về (`data`):
`data` là một mảng các object với cấu trúc như sau:

```json
[
  {
    "student_id": "SE180234",
    "lab_id": "LAB 3 - gRPC & Microservices Architecture",
    "class_name": "SE1815",
    "score": 8.50,
    "status": "Done",
    "updated_at": "2026-07-01T01:45:00.000Z",
    "details": {
      "passed": 2,
      "failed": 1,
      "total": 3,
      "tests": [
        {
          "name": "[SOURCE] Check solution has 3 .csproj files",
          "passed": true,
          "score": 1.0,
          "max_score": 1.0,
          "error": null,
          "actual_response": "Found projects: Domain, Application, Infrastructure. Build OK.",
          "actual_status_code": null
        },
        {
          "name": "[POST] /api/v1/products - Create product",
          "passed": true,
          "score": 2.50,
          "max_score": 2.50,
          "error": null,
          "actual_response": "{\"id\": 1, \"name\": \"Notebook\"}",
          "actual_status_code": 201
        },
        {
          "name": "[GET] /api/v1/products - Get product list",
          "passed": false,
          "score": 0.0,
          "max_score": 1.50,
          "error": "Expected 200 OK, got 500 Internal Server Error",
          "actual_response": "Internal Server Error",
          "actual_status_code": 500
        }
      ]
    }
  }
]
```

### Cấu trúc chi tiết của trường `details` (JSONB):
* `passed` (number): Số lượng test case đạt.
* `failed` (number): Số lượng test case không đạt.
* `total` (number): Tổng số test case.
* `tests` (array): Danh sách chi tiết từng test case:
  * `name` (string): Tên test case (Ví dụ: `[SOURCE] Check folder structure` hoặc `[GET] /api/v1/health - Health check`).
  * `passed` (boolean): `true` nếu pass, `false` nếu fail.
  * `score` (number): Điểm số đạt được trong test case này.
  * `max_score` (number): Điểm số tối đa của test case này.
  * `error` (string|null): Thông báo lỗi chi tiết khi test case thất bại (nếu pass sẽ là `null`).
  * `actual_response` (string|null): Response body nhận được từ server (đối với HTTP tests) hoặc thông tin kết quả chi tiết của test (đối với SOURCE tests).
  * `actual_status_code` (number|null): HTTP status code thực tế trả về từ api (ví dụ: `200`, `500`, `404`). Nếu test case không liên quan đến HTTP (như SOURCE test), trường này sẽ có giá trị `null`.

---

## 3) Handling Errors

Khi truy vấn bằng Supabase SDK, lỗi sẽ được trả về trong object `error`:

| Lỗi phổ biến | Cách xử lý / Nguyên nhân |
|--------------|-------------------------|
| `error.code = 'PGRST116'` | Không tìm thấy dòng nào khớp với MSSV (sinh viên chưa có bài nộp/chưa chấm). |
| `error.message` | Chứa chi tiết lỗi cú pháp hoặc lỗi kết nối mạng. |

---

## 4) FE notes

* **Row Level Security (RLS)**: Bảng `submissions` trên Supabase nên được cấu hình chính sách RLS (Row Level Security) sao cho Sinh viên chỉ có quyền `SELECT` các dòng có `student_id` trùng với MSSV của mình để bảo mật thông tin.
* **Giao diện hiển thị gợi ý (UX)**:
  * Hiển thị danh sách các bài Lab dạng bảng hoặc card.
  * Mỗi bài Lab hiển thị: Tên bài Lab (`lab_id`), Lớp học (`class_name`), Điểm số đạt được (`score`), Trạng thái (`status`), Ngày chấm (`updated_at`).
  * Cho phép sinh viên bấm vào từng bài Lab để bung rộng (Accordion) xem chi tiết danh sách các test case trong mảng `details.tests` để biết mình bị sai ở test case nào và lỗi chi tiết là gì.
