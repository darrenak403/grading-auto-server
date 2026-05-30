# FE Handoff: Lab Assignment Export (Excel)

> Branch: `dev`
> Date: 2026-05-29
> Base URL: `http://{host}/api/v1`

## 1) Endpoint map

- `POST /lab-assignments/{id}/exports` — tạo job export Excel điểm lab assignment
- `GET /exports/{id}` — poll trạng thái job (dùng chung với export exam/assignment)
- `GET /exports/{id}/download` — tải file `.xlsx` khi job `Done`

## 2) Contracts

### Tạo export job cho lab assignment

`POST /lab-assignments/{id}/exports`

**Auth:** Không thay đổi so với các endpoint `lab-assignments` hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab assignment id |

**Body:** Không bắt buộc. Có thể gửi `{}` hoặc bỏ body.

**Response `data`:** `ExportJobDto`

| Field | Type | Note |
|-------|------|------|
| `id` | `guid` | export job id — lưu để poll/download |
| `labAssignmentId` | `guid` | lab assignment được export |
| `labAssignmentTitle` | `string` | tiêu đề lab (hiển thị UI) |
| `assignmentId` | `guid \| null` | `null` với lab export |
| `assignmentCode` | `string \| null` | `null` với lab export |
| `examSessionId` | `guid \| null` | `null` với lab export |
| `examSessionTitle` | `string \| null` | `null` với lab export |
| `status` | `string` | `Pending` \| `Done` \| `Failed` |
| `gradingRound` | `string \| null` | `null` — lab export không filter round |
| `filePath` | `string \| null` | path server-side; FE không cần dùng |
| `errorMessage` | `string \| null` | có giá trị khi `status = Failed` |

**Response envelope example:**

```json
{
  "status": true,
  "message": "Lab export job created.",
  "data": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "labAssignmentId": "8014964c-fd2b-4569-9104-fb249b2ee46d",
    "labAssignmentTitle": "PRN232-Lab1",
    "assignmentId": null,
    "assignmentCode": null,
    "examSessionId": null,
    "examSessionTitle": null,
    "status": "Pending",
    "gradingRound": null,
    "filePath": null,
    "errorMessage": null
  }
}
```

---

### Poll trạng thái export job

`GET /exports/{id}`

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | export job id từ bước tạo |

**Response `data`:** cùng shape `ExportJobDto` ở trên.

**Status lifecycle:**

| `status` | FE action |
|----------|-----------|
| `Pending` | tiếp tục poll |
| `Done` | gọi download |
| `Failed` | hiển thị `errorMessage`, dừng poll |

---

### Tải file Excel

`GET /exports/{id}/download`

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | export job id |

**Response:** binary file

- Content-Type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Filename: `{exportJobId}.xlsx`

**Excel columns (lab):**

| Cột | Nội dung |
|-----|----------|
| `Tên` | phần tên từ `studentCode` |
| `MSSV` | mã SV parse từ `studentCode` |
| `{test case label}` | điểm từng test case `Approved` (theo `order`); label = `description` hoặc `SOURCE` hoặc `{httpMethod} {urlTemplate}` |
| `Grand Total` | `{totalScore}/{maxScore}` |
| `Status` | `Pending` \| `Grading` \| `Done` \| `BuildFailed` \| `Error` |

Điểm mỗi test case = `manualOverrideScore ?? awardedScore` từ latest grading job của submission.

## 3) Error codes

| HTTP | code | message |
|------|------|---------|
| `404` | — | `LabAssignment '{id}' not found.` |
| `404` | — | `Export job '{id}' not found.` |
| `404` | — | `Export not ready or not found.` (download khi job chưa `Done`) |
| `500` | — | `An unexpected error occurred.` (lỗi worker không mong đợi) |

Envelope lỗi:

```json
{
  "status": false,
  "message": "LabAssignment '...' not found.",
  "data": null,
  "errors": null,
  "traceId": "..."
}
```

Khi worker fail, poll `GET /exports/{id}` trả `status: "Failed"` và `errorMessage` cụ thể — không cần parse từ HTTP 500.

## 4) FE notes

- Luồng gợi ý: `POST /lab-assignments/{id}/exports` → poll `GET /exports/{jobId}` mỗi **3–5s** → khi `Done` gọi `GET /exports/{jobId}/download`.
- Export chạy **async** trên worker; tạo job trả về ngay với `status = Pending`.
- Nên disable nút Export khi assignment chưa có submission, hoặc vẫn cho export (file sẽ chỉ có header).
- Có thể tái dùng UI/logic export exam hiện tại — chỉ khác endpoint tạo job và field `labAssignmentId`/`labAssignmentTitle` trong response.
- Download dùng `window.open`, `<a download>`, hoặc `fetch` + blob; không parse JSON.
- Poll interval: dừng sau **2 phút** hoặc khi `Done`/`Failed`; hiển thị spinner + `errorMessage` nếu fail.
