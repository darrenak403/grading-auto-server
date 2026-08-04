# FE Handoff: Lab Assignment Roster & Per-Submission Progress

> Branch: `dev`
> Date: 2026-05-28
> Base URL: `http://{host}/api/v1`

## 1) Endpoint map

- `GET /lab-assignments/{id}/roster` — lấy danh sách bài nộp trong lab kèm điểm tổng quan từng sinh viên
- `GET /lab-assignments/{id}/grading-progress` — lấy tiến trình của bài đang chấm hiện tại + queue

## 2) Contracts

### Roster của lab assignment

`GET /lab-assignments/{id}/roster`

**Auth:** Không thay đổi so với các endpoint `lab-assignments` hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab assignment id |

**Response `data`:** `LabAssignmentRosterItemDto[]` (sort theo `studentCode`)

| Field | Type | Note |
|------|------|------|
| `submissionId` | `guid` | id bài nộp |
| `studentCode` | `string` | mã sinh viên |
| `originalFileName` | `string` | tên file nộp gốc |
| `submissionStatus` | `string` | `Pending` \| `Grading` \| `Done` \| `BuildFailed` \| `Error` |
| `latestJobId` | `guid \| null` | job gần nhất của submission |
| `jobStatus` | `string \| null` | `Pending` \| `Running` \| `Done` \| `Failed` |
| `totalScore` | `number \| null` | chỉ có khi latest job là `Done/Failed` và có result; đang chấm thì `null` |
| `maxScore` | `number` | tổng điểm test case `Approved` của lab |
| `createdAt` | `string (ISO 8601)` | thời gian tạo submission |
| `updatedAt` | `string (ISO 8601) \| null` | thời gian cập nhật |

---

### Progress theo từng bài đang chấm

`GET /lab-assignments/{id}/grading-progress`

**Auth:** Không thay đổi so với các endpoint `lab-assignments` hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab assignment id |

**Response `data`:** `LabGradingProgressDto`

| Field | Type | Note |
|------|------|------|
| `assignmentId` | `guid` | id lab assignment |
| `assignmentStatus` | `string` | `Draft` \| `TestcasesReady` \| `Grading` \| `Done` |
| `runningSubmissionId` | `guid \| null` | submission đang hiển thị tiến trình |
| `runningStudentCode` | `string \| null` | mã SV đang chấm |
| `runningJobId` | `guid \| null` | grading job hiện tại |
| `runningJobStatus` | `string \| null` | `Pending` \| `Running` \| `Done` \| `Failed` |
| `runningPercent` | `number` | `%` bài hiện tại, tính theo `executedTestCaseCount / totalTestCaseCount`, clamp 0..100 |
| `executedTestCaseCount` | `number` | số test case đã có result của `runningJobId` |
| `totalTestCaseCount` | `number` | tổng test case `Approved` của lab |
| `queuedSubmissionCount` | `number` | số submission còn chờ phía sau |
| `completedSubmissionCount` | `number` | số submission có latest job `Done/Failed` |
| `isGradingActive` | `boolean` | `true` nếu còn item `Running/Pending`, ngược lại `false` |

**Behavior note:**
- API ưu tiên item `Running`; nếu chưa có thì chọn item `Pending` sớm nhất.
- `runningPercent = 0` khi đang Pending hoặc lab chưa có test case `Approved`.
- FE poll sau `POST /lab-assignments/{id}/grade` hoặc `POST /lab-submissions/regrade-all?assignmentId={id}`.

## 3) Error codes

| HTTP | code | message |
|------|------|---------|
| `404` | `NOT_FOUND` | `LabAssignment '{id}' not found.` |

## 4) FE notes

- Dùng `roster` để render bảng điểm chính; không cần gọi N lần `GET /lab-submissions/{id}/results`.
- Với `totalScore = null`, UI nên hiển thị trạng thái “Đang chấm” thay vì `0`.
- Với progress API, khi `runningSubmissionId` đổi thì coi như chuyển sang bài kế tiếp.
- Poll interval gợi ý: `2s` trong lúc `isGradingActive = true`; dừng poll khi `false`.
