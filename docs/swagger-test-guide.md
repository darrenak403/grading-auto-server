# Hướng dẫn test API tuần tự trên Swagger

URL: http://localhost:5049/swagger

Chạy đúng thứ tự bên dưới. Copy ID từ response trước vào request sau.

---

## Bước 1 — Tạo Semester

**`POST /api/v1/semesters`**

```json
{
  "name": "Fall 2026",
  "code": "FA26",
  "startDate": "2026-09-01",
  "endDate": "2026-12-31"
}
```

✅ Lấy `data.id` → gọi là `{semesterId}`

---

## Bước 2 — Tạo Lab Assignment

**`POST /api/v1/lab-assignments`**

```json
{
  "title": "PRN232 - Lab 1: REST API Basics",
  "description": "3-layer API + Docker Compose",
  "semesterId": "{semesterId}"
}
```

✅ Lấy `data.id` → gọi là `{assignmentId}`

---

## Bước 3 — Import test case (batch)

**`POST /api/v1/lab-assignments/{assignmentId}/testcases/batch`**

Paste mảng JSON test case vào body. Ví dụ tối thiểu:

```json
[
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-count:3",
    "description": "Phải có đúng 3 project (3-layer architecture)",
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 2.0,
    "order": 1
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-exists:**/docker-compose.yml",
    "description": "Phải có file docker-compose.yml",
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0,
    "order": 2
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/products",
    "description": "Lấy danh sách sản phẩm — trả về 200",
    "inputJson": null,
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 1.0,
    "order": 3
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/products",
    "description": "Tạo sản phẩm mới",
    "inputJson": "{\"name\":\"Test Product\",\"price\":100}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 1.0,
    "order": 4
  }
]
```

✅ Response trả về danh sách test case mới tạo, tất cả `status = "Draft"`

✅ Lấy `data[0].id`, `data[1].id`, ... → gọi là `{testCaseId}`

---

## Bước 4 — Xem danh sách test case

**`GET /api/v1/lab-assignments/{assignmentId}/testcases`**

Kiểm tra test case đã đủ, đúng nội dung chưa.

---

## Bước 5 — Approve test case

Approve từng test case muốn dùng để chấm:

**`PATCH /api/v1/lab-testcases/{testCaseId}/approve`**

> Không cần body. Gọi lần lượt cho từng `{testCaseId}`.

✅ `data.status` chuyển sang `"Approved"`

> Test case còn `Draft` hoặc `Rejected` sẽ bị bỏ qua khi chấm.

---

## Bước 6 — Upload bài nộp sinh viên

**`POST /api/v1/lab-submissions?assignmentId={assignmentId}`**

Trong Swagger: chọn **"Choose Files"**, upload file ZIP/RAR.

**Quy tắc đặt tên file bắt buộc:**

```
SE180234_NguyenVanA.zip
SE180456_TranThiB.rar
```

Phần trước dấu `_` đầu tiên = mã sinh viên.

✅ Response:

```json
{
  "data": {
    "created": [{"id": "...", "studentCode": "SE180234", "status": "Pending"}],
    "warnings": []
  }
}
```

Nếu `warnings` có nội dung → tên file không đúng định dạng, file đó bị bỏ qua.

✅ Lấy `data.created[0].id` → gọi là `{submissionId}`

---

## Bước 7 — Trigger chấm

**`POST /api/v1/lab-assignments/{assignmentId}/grade`**

> Không cần body.

✅ Response: `{ "jobsCreated": N, "message": "N grading job(s) created." }`

Worker sẽ xử lý bất đồng bộ. Theo dõi log worker ở terminal.

---

## Bước 8 — Theo dõi trạng thái

**`GET /api/v1/lab-submissions?assignmentId={assignmentId}`**

Refresh cho đến khi `status` không còn `"Pending"` hoặc `"Grading"`:

| Status        | Ý nghĩa                                              |
| ------------- | ---------------------------------------------------- |
| `Pending`     | Chưa được chấm                                       |
| `Grading`     | Đang chấm                                            |
| `Done`        | Chấm xong                                            |
| `BuildFailed` | Docker build thất bại (SOURCE test case vẫn có điểm) |
| `Error`       | Lỗi không mong đợi                                   |

---

## Bước 9 — Xem kết quả chi tiết

**`GET /api/v1/lab-submissions/{submissionId}/results`**

Response:

```json
{
  "data": {
    "studentCode": "SE180234",
    "submissionStatus": "Done",
    "totalScore": 4.0,
    "results": [
      {
        "httpMethod": "SOURCE",
        "urlTemplate": "project-count:3",
        "passed": true,
        "awardedScore": 2.0,
        "actualResponse": "Found 3 .csproj files"
      },
      {
        "httpMethod": "GET",
        "urlTemplate": "/api/products",
        "passed": false,
        "awardedScore": 0,
        "actualStatusCode": 404,
        "errorMessage": "..."
      }
    ]
  }
}
```

---

## Bước 10 — Điều chỉnh điểm thủ công (tuỳ chọn)

**`PUT /api/v1/lab-submissions/{submissionId}/adjust`**

```json
{
  "resultId": "{id của result cần sửa}",
  "score": 0.5,
  "reason": "Sinh viên làm đúng nhưng format response khác yêu cầu"
}
```

> `resultId` lấy từ `data.results[i].id` ở bước 9.

---

## Các thao tác khác

### Sửa test case (trước khi approve)

**`PUT /api/v1/lab-testcases/{testCaseId}`**

```json
{
  "httpMethod": "GET",
  "urlTemplate": "/api/products",
  "description": "...",
  "inputJson": null,
  "expectJson": "{\"success\":true}",
  "expectedStatusCode": 200,
  "matchMode": "Subset",
  "score": 1.0,
  "order": 3
}
```

### Reject test case

**`PATCH /api/v1/lab-testcases/{testCaseId}/reject`**

### Xóa test case

**`DELETE /api/v1/lab-testcases/{testCaseId}`** → 204 No Content

### Chấm lại (re-grade)

Trigger lại `POST /grade` — chỉ tạo job mới cho submission chưa có job active.  
Nếu muốn chấm lại bài đã `Done`, cần xóa submission cũ rồi upload lại.

---

## Checklist nhanh

```
[ ] POST /semesters              → lấy semesterId
[ ] POST /lab-assignments        → lấy assignmentId
[ ] POST /testcases/batch        → import test case
[ ] GET  /testcases              → kiểm tra nội dung
[ ] PATCH /testcases/{id}/approve → approve từng cái
[ ] POST /lab-submissions        → upload file ZIP
[ ] POST /grade                  → trigger chấm
[ ] GET  /lab-submissions        → chờ status = Done
[ ] GET  /submissions/{id}/results → xem kết quả
```
