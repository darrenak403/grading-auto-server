# FE Handoff: Lab Grading System

> Branch: `dev`
> Date: 2026-05-27
> Base URL: `http://{host}/api/v1`

---

## 1) Endpoint Map

### Semesters
- `GET /semesters` — danh sách học kỳ
- `GET /semesters/{id}` — chi tiết học kỳ
- `POST /semesters` — tạo học kỳ
- `PUT /semesters/{id}` — cập nhật học kỳ
- `DELETE /semesters/{id}` — xóa học kỳ (204)

### Lab Assignments
- `GET /lab-assignments` — danh sách lab
- `GET /lab-assignments/{id}` — chi tiết lab
- `POST /lab-assignments` — tạo lab mới
- `PUT /lab-assignments/{id}` — cập nhật lab
- `DELETE /lab-assignments/{id}` — xóa lab (204)
- `GET /lab-assignments/{id}/testcases` — lấy danh sách test case của lab
- `POST /lab-assignments/{id}/testcases` — tạo một test case
- `POST /lab-assignments/{id}/testcases/batch` — import nhiều test case cùng lúc (từ JSON)
- `DELETE /lab-assignments/{id}/testcases` — xóa toàn bộ test case của lab (kèm kết quả chấm)
- `POST /lab-assignments/{id}/grade` — trigger chấm tất cả bài chưa chấm

### Lab Test Cases
- `GET /lab-testcases/{id}` — chi tiết test case
- `PUT /lab-testcases/{id}` — cập nhật test case
- `DELETE /lab-testcases/{id}` — xóa test case (204)
- `PATCH /lab-testcases/{id}/status` — đổi trạng thái: `Draft` · `Approved` · `Rejected`
- `PATCH /lab-assignments/{id}/testcases/approve-all` — approve tất cả test case Draft của lab

### Lab Submissions (bài nộp của sinh viên)
- `GET /lab-submissions?assignmentId={id}` — danh sách bài nộp (filter theo lab)
- `GET /lab-submissions/{id}` — chi tiết một bài nộp
- `POST /lab-submissions?assignmentId={id}` — upload hàng loạt file bài nộp
- `DELETE /lab-submissions/{id}` — xóa một bài nộp (kèm file)
- `DELETE /lab-submissions?assignmentId={id}` — xóa toàn bộ bài nộp của lab (kèm file)
- `GET /lab-submissions/{id}/results` — kết quả chấm chi tiết của một bài
- `POST /lab-submissions/{id}/regrade` — chấm lại một bài (hủy job cũ, tạo job mới)
- `POST /lab-submissions/regrade-all?assignmentId={id}` — chấm lại **toàn bộ** bài nộp của lab
- `PUT /lab-submissions/{id}/adjust` — điều chỉnh điểm thủ công

---

## 2) Response Wrapper

Tất cả response đều bọc trong:

```json
{
  "status": true,
  "message": "Success",
  "data": { ... },
  "errors": null,
  "traceId": null
}
```

- `status: false` → request thất bại, đọc `message` và `errors[]`
- `traceId` chỉ có khi lỗi (dùng để tra log)
- Delete trả về `204 No Content` (không có body)

---

## 3) Contracts

### Semesters

#### `GET /semesters`

**Response `data`:** `SemesterDto[]`

| Field | Type | Note |
|-------|------|------|
| `id` | `string (guid)` | |
| `name` | `string` | Tên học kỳ, vd: "Fall 2026" |
| `code` | `string` | Mã, vd: "FA26" |
| `startDate` | `string (YYYY-MM-DD)` \| `null` | |
| `endDate` | `string (YYYY-MM-DD)` \| `null` | |
| `labAssignmentCount` | `number` | Số lab trong học kỳ |
| `createdAt` | `string (ISO 8601)` | |
| `updatedAt` | `string (ISO 8601)` \| `null` | |

#### `POST /semesters` · `PUT /semesters/{id}`

**Body:**
```json
{
  "name": "Fall 2026",
  "code": "FA26",
  "startDate": "2026-09-01",
  "endDate": "2026-12-31"
}
```

| Field | Type | Required |
|-------|------|----------|
| `name` | string | ✓ |
| `code` | string | ✓ |
| `startDate` | `YYYY-MM-DD` \| null | optional |
| `endDate` | `YYYY-MM-DD` \| null | optional |

---

### Lab Assignments

#### `GET /lab-assignments`

**Response `data`:** `LabAssignmentDto[]`

| Field | Type | Note |
|-------|------|------|
| `id` | `string (guid)` | |
| `semesterId` | `string (guid)` \| `null` | |
| `semesterName` | `string` \| `null` | Tên học kỳ |
| `title` | `string` | Tên lab, vd: "PRN232 - Lab 1" |
| `description` | `string` \| `null` | Mô tả yêu cầu |
| `status` | `string` | Enum: xem bên dưới |
| `testCaseCount` | `number` | Tổng số test case |
| `submissionCount` | `number` | Số bài đã nộp |
| `createdAt` | `string (ISO 8601)` | |
| `updatedAt` | `string (ISO 8601)` \| `null` | |

**Status values:** `Active` · `Archived`

#### `POST /lab-assignments` · `PUT /lab-assignments/{id}`

**Body:**
```json
{
  "title": "PRN232 - Lab 1: REST API Basics",
  "description": "Yêu cầu build 3-layer API với Docker Compose",
  "semesterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

| Field | Type | Required |
|-------|------|----------|
| `title` | string | ✓ |
| `description` | string \| null | optional |
| `semesterId` | guid \| null | optional |

---

### Lab Test Cases

#### `GET /lab-assignments/{id}/testcases`

**Response `data`:** `LabTestCaseDto[]`

| Field | Type | Note |
|-------|------|------|
| `id` | `string (guid)` | |
| `labAssignmentId` | `string (guid)` | |
| `httpMethod` | `string` | `GET` · `POST` · `PUT` · `DELETE` · `PATCH` · `SOURCE` |
| `urlTemplate` | `string` | Path API hoặc SOURCE rule (xem bên dưới) |
| `description` | `string` \| `null` | Mô tả test case |
| `inputJson` | `object` \| `string` \| `null` | JSON body gửi lên — raw object hoặc string |
| `expectJson` | `object` \| `string` \| `null` | JSON kỳ vọng — raw object hoặc string |
| `expectedStatusCode` | `number` | Mặc định 200 |
| `matchMode` | `string` | `Subset` · `Exact` · `StatusOnly` |
| `score` | `number` | Điểm của test case này |
| `status` | `string` | `Draft` · `Approved` · `Rejected` |
| `aiGenerated` | `boolean` | Luôn `false` (AI đã bị loại bỏ) |
| `order` | `number` | Thứ tự chạy |
| `createdAt` | `string (ISO 8601)` | |
| `updatedAt` | `string (ISO 8601)` \| `null` | |

**matchMode giải thích:**
- `Subset` — response JSON phải chứa tất cả key-value trong `expectJson` (bỏ qua field thừa)
- `Exact` — response JSON phải khớp hoàn toàn
- `StatusOnly` — chỉ kiểm tra HTTP status code, bỏ qua body

#### `POST /lab-assignments/{id}/testcases` — Tạo một test case

**Body:**
```json
{
  "httpMethod": "GET",
  "urlTemplate": "/api/products",
  "description": "Lấy danh sách sản phẩm",
  "inputJson": null,
  "expectJson": {"success": true},
  "expectedStatusCode": 200,
  "matchMode": "Subset",
  "score": 1.0,
  "order": 1
}
```

| Field | Type | Required | Default |
|-------|------|----------|---------|
| `httpMethod` | string | ✓ | — |
| `urlTemplate` | string | ✓ | — |
| `description` | string \| null | optional | null |
| `inputJson` | object \| null | optional | null |
| `expectJson` | object \| null | optional | null |
| `expectedStatusCode` | number | optional | `200` |
| `matchMode` | string | optional | `"Subset"` |
| `score` | number | optional | `1.0` |
| `order` | number | optional | `0` |

> **`inputJson` / `expectJson`:** Truyền trực tiếp raw JSON object `{"key":"val"}` hoặc `null`. API tự xử lý, không cần stringify hay escape.

#### `POST /lab-assignments/{id}/testcases/batch` — Import hàng loạt

**Body:** Array của cùng schema trên
```json
[
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/products",
    "description": "Lấy danh sách sản phẩm",
    "inputJson": null,
    "expectJson": {"success": true},
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/products",
    "description": "Tạo sản phẩm mới",
    "inputJson": {"name": "Test Product", "price": 100},
    "expectJson": {"success": true},
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-count:3",
    "description": "Phải có đúng 3 project (3-layer architecture)",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 2.0
  }
]
```

**Response `data`:** `LabTestCaseDto[]` (danh sách test case vừa tạo)

> **Lưu ý:** Test case mới tạo luôn ở trạng thái `Draft`. Phải approve trước khi trigger grading.

#### `DELETE /lab-assignments/{id}/testcases` — Xóa toàn bộ test case

Không cần body. Xóa tất cả test case của lab đó khỏi DB (kèm toàn bộ kết quả chấm liên quan).

**Response `data`:**
```json
{ "deleted": 13 }
```

> Dùng khi cần import lại test case từ đầu (xóa rồi batch import lại).

#### SOURCE test case — kiểm tra kiến trúc source code

Khi `httpMethod = "SOURCE"`, `urlTemplate` là một rule kiểm tra source code của sinh viên:

| Rule | Ví dụ urlTemplate | Kiểm tra |
|------|-------------------|---------|
| `project-name:GLOB` | `project-name:PRN232.*.API` | Tên .csproj khớp glob |
| `project-count:N` | `project-count:3` | Đúng N project (N-layer arch) |
| `folder-exists:GLOB` | `folder-exists:**/Controllers` | Folder tồn tại |
| `file-exists:GLOB` | `file-exists:**/docker-compose.yml` | File tồn tại |
| `file-contains:GLOB:TEXT` | `file-contains:**/Services/*.cs:IRepository` | File chứa text |
| `file-not-contains:GLOB:TEXT` | `file-not-contains:**/Controllers/*.cs:DbContext` | File không chứa text |

SOURCE checks chạy **trước** Docker (sinh viên vẫn có điểm kiến trúc dù Docker build lỗi).

---

### Đổi trạng thái test case (từng cái)

#### `PATCH /lab-testcases/{id}/status`

**Body:**
```json
{ "status": "Approved" }
```

| Giá trị `status` | Ý nghĩa |
|-----------------|---------|
| `Draft` | Reset về nháp (bị bỏ qua khi chấm) |
| `Approved` | Duyệt — sẽ chạy khi trigger grade |
| `Rejected` | Từ chối — bị bỏ qua khi chấm |

**Response `data`:** `LabTestCaseDto` (test case sau khi cập nhật)

#### `PATCH /lab-assignments/{id}/testcases/approve-all`

Không cần body. Approve toàn bộ test case đang `Draft` của lab đó trong một lần.

**Response `data`:**
```json
{ "approved": 29, "message": "29 test case(s) approved." }
```

Nếu không có Draft nào: `approved: 0`.

---

### Lab Submissions

#### `POST /lab-submissions?assignmentId={id}` — Upload bài nộp

**Content-Type:** `multipart/form-data`

**Form fields:** `files` (multiple files)

**Quy tắc đặt tên file:** `Lab{N}_{MaSV}.zip` (hoặc `.rar`)
- Phần **sau** dấu `_` đầu tiên = mã sinh viên
- Ví dụ: `Lab1_SE180234.zip`, `Lab1_SE180456.rar`

**Response `data`:**
```json
{
  "created": [
    {
      "id": "...",
      "labAssignmentId": "...",
      "studentCode": "SE180234",
      "originalFileName": "SE180234_NguyenVanA.zip",
      "status": "Pending",
      "createdAt": "2026-05-27T10:00:00Z",
      "updatedAt": null
    }
  ],
  "warnings": [
    "Skipped 'NoUnderscore.zip': filename must contain '_' separator (e.g. Lab1_SE180234.zip)"
  ]
}
```

**Submission status values:** `Pending` · `Grading` · `Done` · `BuildFailed` · `Error`

#### `GET /lab-submissions?assignmentId={id}`

**Response `data`:** `LabSubmissionDto[]` — mảng bài nộp, sắp xếp theo mã sinh viên

#### `GET /lab-submissions/{id}/results` — Kết quả chấm

**Response `data`:**
```json
{
  "submissionId": "...",
  "studentCode": "SE180234",
  "submissionStatus": "Done",
  "latestJobId": "...",
  "jobStatus": "Done",
  "totalScore": 8.5,
  "results": [
    {
      "id": "...",
      "labTestCaseId": "...",
      "httpMethod": "GET",
      "urlTemplate": "/api/products",
      "passed": true,
      "awardedScore": 1.0,
      "actualStatusCode": 200,
      "actualResponse": "{\"success\":true,...}",
      "errorMessage": null,
      "manualOverrideScore": null,
      "overrideReason": null
    }
  ]
}
```

#### `DELETE /lab-submissions/{id}` — Xóa một bài nộp

Không cần body. Xóa bài nộp khỏi DB và xóa file trên disk.

**Response `data`:** `{}`

---

#### `DELETE /lab-submissions?assignmentId={id}` — Xóa toàn bộ bài nộp của lab

Không cần body. Xóa tất cả submission của assignment đó kèm file trên disk.

**Response `data`:**
```json
{ "deleted": 25 }
```

> Dùng khi cần upload lại toàn bộ bài sau khi phát hiện lỗi.

---

#### `POST /lab-submissions/{id}/regrade` — Chấm lại một bài

Không cần body. Tạo job mới ở cuối hàng đợi. Nếu submission đã có job `Pending` thì không tạo thêm job trùng; job `Running` hiện tại vẫn chạy xong trước.

**Response `data`:**
```json
{
  "queued": true,
  "message": "Regrade job queued. Worker will process it sequentially."
}
```

> Dùng khi một bài bị kẹt ở `Error` hoặc `BuildFailed` và muốn chấm lại.

---

#### `POST /lab-submissions/regrade-all?assignmentId={id}` — Chấm lại toàn bộ

Không cần body. Tạo job mới cho các bài chưa có job `Pending`; job đang `Running` không bị hủy và toàn bộ hàng đợi được xử lý tuần tự.

**Query param:** `assignmentId` (guid) — bắt buộc

**Response `data`:**
```json
{ "queued": 25 }
```

> Dùng sau khi sửa test case hoặc deploy code mới của worker — regrade toàn lớp chỉ một lần gọi.

---

#### `PUT /lab-submissions/{id}/adjust` — Điều chỉnh điểm thủ công

**Body:**
```json
{
  "resultId": "guid-của-LabTestCaseResult",
  "score": 0.5,
  "reason": "Sinh viên làm đúng nhưng format response khác"
}
```

---

### Trigger Grading

#### `POST /lab-assignments/{id}/grade`

Alias tương đương: `POST /lab-assignments/{id}/grade-all`.

Không cần body. Tạo grading job cho tất cả submission chưa có job active. Worker xử lý tuần tự: một bài phải hoàn tất, lưu kết quả, cleanup Docker xong rồi mới lấy bài kế tiếp.

**Response `data`:**
```json
{
  "jobsCreated": 25,
  "message": "25 grading job(s) created."
}
```

Nếu không có bài nào cần chấm: `jobsCreated: 0`.

---

## 4) Error Codes

| HTTP | Tình huống |
|------|-----------|
| `400` | Body sai / `matchMode` không hợp lệ / tên file không có `_` / filename path traversal |
| `404` | ID không tồn tại (semester, lab assignment, test case, submission) |
| `500` | Lỗi server — xem `traceId` trong response để tra log |

**Error response shape:**
```json
{
  "status": false,
  "message": "LabAssignment 'abc...' not found.",
  "data": null,
  "errors": ["..."],
  "traceId": "00-abc123..."
}
```

---

## 5) FE Notes & Workflow

### Quy trình chuẩn để chấm một lab

```
1. Tạo Semester (nếu chưa có)
2. Tạo LabAssignment → gắn SemesterId
3. Import test case batch (JSON từ ChatGPT/Gemini)
4. Approve: PATCH /lab-assignments/{id}/testcases/approve-all (hoặc từng cái qua /status)
5. Upload bài nộp sinh viên (multipart, nhiều file ZIP)
6. POST /lab-assignments/{id}/grade → trigger chấm
7. Poll GET /lab-submissions?assignmentId={id} cho đến khi status ≠ Pending/Grading
8. Xem kết quả từng bài: GET /lab-submissions/{id}/results
```

**Nếu cần upload lại toàn bộ:**
```
DELETE /lab-submissions?assignmentId={id}   ← xóa sạch bài cũ + file
POST   /lab-submissions?assignmentId={id}   ← upload lại
POST   /lab-assignments/{id}/grade          ← chấm lại
```

**Nếu một bài bị Error và muốn chấm lại:**
```
POST /lab-submissions/{id}/regrade
```

**Nếu muốn chấm lại toàn bộ bài nộp của lab (vd: sau khi sửa test case):**
```
POST /lab-submissions/regrade-all?assignmentId={id}
```

### Polling kết quả

Worker chấm bất đồng bộ — sau khi trigger grade, FE nên poll kết quả:

```
GET /lab-submissions?assignmentId={id}
→ kiểm tra tất cả items có status != "Pending" && != "Grading"
→ nếu tất cả Done/BuildFailed/Error thì dừng poll
```

Hoặc poll từng submission: `GET /lab-submissions/{id}` → check `status`.

### Test case status flow

```
Draft → PATCH /status {"status":"Approved"} → Approved → chạy khi grade
      → PATCH /status {"status":"Rejected"} → Rejected → bị bỏ qua
      ← PATCH /status {"status":"Draft"}    ← reset về Draft từ bất kỳ trạng thái
```

Approve nhanh toàn bộ: `PATCH /lab-assignments/{id}/testcases/approve-all` (chỉ approve Draft, không động đến Rejected).

Chỉ test case `Approved` mới được dùng khi chấm.

### File upload convention

- Tên file bắt buộc: `Lab{N}_{MaSV}.zip|.rar|...`
- Phần **sau** `_` đầu tiên = `studentCode` (dùng để nhận diện sinh viên)
- Ví dụ: `Lab1_SE180234.zip` → `studentCode = "SE180234"`
- Nếu upload nhiều file cùng `studentCode` → file sau bị skip, warning được trả về

### SOURCE vs HTTP test cases

Khi hiển thị kết quả:
- `httpMethod = "SOURCE"` → đây là kiểm tra kiến trúc source code, không phải HTTP call
- `actualResponse` của SOURCE sẽ là mô tả kết quả kiểm tra (vd: "Found 3 .csproj files matching pattern")
- SOURCE test cases cho điểm ngay cả khi Docker build thất bại (`BuildFailed`)

### Score calculation

`totalScore = sum(awardedScore)` cho tất cả `results`.
Nếu có `manualOverrideScore != null` → dùng `manualOverrideScore` thay vì `awardedScore`.

> FE nên tự tính `effectiveScore = manualOverrideScore ?? awardedScore` cho mỗi result row khi hiển thị tổng điểm.
