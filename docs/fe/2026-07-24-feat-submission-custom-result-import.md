# FE Handoff: Submission Custom Result Import

> Branch: `tvu`
> Date: 2026-07-24
> Base URL: `http://{host}/api/v1`

Tài liệu này dành cho FE khi cần set điểm custom cho một normal submission bằng cách copy result mẫu từ một submission đã đạt full score, ví dụ set bài hiện tại về `8/10`.

## 1) Endpoint map

- `PUT /submissions/{id}/custom-result` - copy latest full-score result từ một submission mẫu cùng assignment, scale về điểm custom, và lưu result mới cho target submission.
- `PUT /lab-submissions/{id}/custom-result` - copy latest full-score lab result từ một lab submission mẫu cùng lab assignment, giữ response/pass/error detail từ template và scale effective score về điểm custom.

## 2) Contracts

### Import custom result

`PUT /api/v1/submissions/{id}/custom-result`

**Auth:** Public / theo cấu hình API hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Target submission id cần chỉnh điểm. |

**Query params:** Không có.

**Body:**

```json
{
  "templateSubmissionId": "11111111-1111-1111-1111-111111111111",
  "score": 8,
  "reason": "Custom score by teacher",
  "adjustedBy": "teacher@example.com"
}
```

Validation rules:

- `templateSubmissionId` required, phải là submission khác target submission.
- Template submission và target submission phải thuộc cùng `assignmentId`.
- Template phải có latest completed result đạt full score, ví dụ `10/10`.
- `score` phải nằm trong range `0..maxScore`; với bài 10 điểm thì gửi `8` để ra `8/10`.
- `reason` required, trim ở backend, dài tối đa 1000 ký tự.
- `adjustedBy` optional, trim ở backend, dài tối đa 200 ký tự.
- Target submission không được có grading job đang `Pending` hoặc `Running`.
- API sẽ xóa question results cũ của target submission và tạo một grading job `Done` mới chứa result custom.

**Response envelope:**

```json
{
  "status": true,
  "message": "Custom result imported.",
  "data": []
}
```

**Response `data`:** `QuestionResultDto[]`

| Field | Type | Note |
|-------|------|------|
| `id` | `guid` | Question result id mới. |
| `submissionId` | `guid` | Target submission id. |
| `questionId` | `guid` | Question id được copy từ template result. |
| `questionTitle` | `string` | Có thể rỗng ở response này; FE không nên phụ thuộc field này để render title. |
| `studentCode` | `string` | Student code của target submission. |
| `studentId` | `string` | Hiện thường rỗng với normal submission result. |
| `score` | `number` | Điểm đã scale cho từng question. |
| `maxScore` | `number` | Max score của question. |
| `finalScore` | `number` | Điểm cuối cùng; bằng `adjustedScore` nếu có adjust. |
| `testCaseResults` | `Array \| null` | Test cases được copy từ template và scale `awardedScore`. |
| `adjustedScore` | `number \| null` | Điểm custom theo từng question. |
| `adjustReason` | `string \| null` | Lý do từ `reason`. |
| `adjustedBy` | `string \| null` | Người chỉnh từ `adjustedBy`. |
| `adjustedAt` | `string \| null` | ISO datetime backend set lúc import. |

Example success:

```json
{
  "status": true,
  "message": "Custom result imported.",
  "data": [
    {
      "id": "22222222-2222-2222-2222-222222222222",
      "submissionId": "33333333-3333-3333-3333-333333333333",
      "questionId": "44444444-4444-4444-4444-444444444444",
      "questionTitle": "",
      "studentCode": "SE181234",
      "studentId": "",
      "score": 8,
      "maxScore": 10,
      "finalScore": 8,
      "testCaseResults": [],
      "adjustedScore": 8,
      "adjustReason": "Custom score by teacher",
      "adjustedBy": "teacher@example.com",
      "adjustedAt": "2026-07-24T09:30:00Z"
    }
  ]
}
```

## 3) Error codes

| HTTP | code | Message |
|------|------|---------|
| `400` | `BAD_REQUEST` | `Template submission must be different from the target submission.` |
| `400` | `BAD_REQUEST` | `Template and target submissions must belong to the same assignment.` |
| `400` | `BAD_REQUEST` | `Reason is required.` |
| `400` | `BAD_REQUEST` | `Submission is already being graded.` |
| `400` | `BAD_REQUEST` | `Template submission has no completed results.` |
| `400` | `BAD_REQUEST` | `Template submission must have a full-score result.` |
| `400` | `BAD_REQUEST` | `Score must be in range [0..{maxScore}].` |
| `400` | `BAD_REQUEST` | `Template submission contains invalid test-case result data.` |
| `400` | `VALIDATION_ERROR` | Model validation error, ví dụ thiếu `templateSubmissionId`, thiếu `reason`, hoặc `reason` quá dài. |
| `404` | `NOT_FOUND` | `Submission '{id}' not found.` |
| `404` | `NOT_FOUND` | `Template submission '{templateSubmissionId}' not found.` |

Error envelope:

```json
{
  "status": false,
  "message": "Reason is required.",
  "errors": null,
  "traceId": "..."
}
```

## 4) FE function mẫu

```ts
type ApiResponse<T> = {
  status: boolean
  message: string
  data?: T
  errors?: string[]
  traceId?: string
}

type QuestionResult = {
  id: string
  submissionId: string
  questionId: string
  questionTitle: string
  studentCode: string
  studentId: string
  score: number
  maxScore: number
  finalScore: number
  testCaseResults: unknown[] | null
  adjustedScore: number | null
  adjustReason: string | null
  adjustedBy: string | null
  adjustedAt: string | null
}

type ImportCustomResultInput = {
  submissionId: string
  templateSubmissionId: string
  score: number
  reason: string
  adjustedBy?: string
}

export async function importSubmissionCustomResult(
  apiBaseUrl: string,
  input: ImportCustomResultInput,
): Promise<QuestionResult[]> {
  const response = await fetch(
    `${apiBaseUrl}/submissions/${input.submissionId}/custom-result`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        templateSubmissionId: input.templateSubmissionId,
        score: input.score,
        reason: input.reason,
        adjustedBy: input.adjustedBy,
      }),
    },
  )

  const payload = (await response.json()) as ApiResponse<QuestionResult[]>

  if (!response.ok || !payload.status) {
    throw new Error(payload.message || 'Import custom result failed')
  }

  return payload.data ?? []
}
```

## 5) FE notes

- Flow UI đề xuất: chọn target submission -> chọn template submission đang `10/10` cùng assignment -> nhập điểm custom, ví dụ `8` -> nhập reason -> gọi endpoint.
- FE nên disable action nếu target đang `Pending`/`Running`, hoặc xử lý lỗi `Submission is already being graded.` từ backend.
- Sau khi gọi thành công, refresh lại row target bằng `GET /api/v1/submissions/{id}/results` hoặc endpoint table hiện tại để cập nhật total score.
- Không cần FE tự scale từng question/testcase; backend đã scale score và testcase awarded score theo tỉ lệ `score / maxScore`.
- Vì API thay thế results cũ của target submission, nên nên có confirm dialog trước khi submit.

## 6) Lab custom result

`PUT /api/v1/lab-submissions/{id}/custom-result`

Body:

```json
{
  "score": 8,
  "reason": "Custom total score adjustment to 8/10"
}
```

Response `data`: `LabGradingResultDto`.

FE notes:

- Dùng endpoint này cho màn Lab `Review Results`, không loop `PUT /lab-submissions/{id}/adjust` từng testcase.
- `templateSubmissionId` optional. Không gửi field này để dùng built-in sample result từ approved testcases.
- Nếu user chọn một bài 10/10 làm source thì gửi thêm `templateSubmissionId`; backend copy result detail từ bài đó.
- Backend tạo lab grading job `Done` mới cho target submission.
- Built-in sample sẽ set `passed = true`, `actualStatusCode = expectedStatusCode`, `actualResponse = expectJson` nếu testcase có expected body.
- Nếu có `templateSubmissionId`, backend sẽ trừ điểm vào khoảng 2-3 API testcase nghiệp vụ điểm cao nhất nếu đủ capacity; tránh auth bootstrap như `/auth/login`, `/auth/refresh-token`, `/auth/register`. Testcase bị trừ hiển thị như lỗi API thường: `actualStatusCode = 404`, `errorMessage = "Expected status {expected}, got 404."`.
- Backend set `manualOverrideScore` để total score bằng `score`; ví dụ `8/10`.
- Sau success, FE có thể set detail bằng response `data` và refresh roster bằng `GET /api/v1/lab-assignments/{id}/roster`.
