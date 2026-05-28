# FE Handoff: Lab Grading API Usage

> Branch: `dev`
> Date: 2026-05-29
> Base URL: `http://{host}/api/v1`

Tài liệu này chỉ tập trung vào nhóm API chấm điểm Lab và cách FE nên dùng để tối ưu queue, progress, tài nguyên Docker, và tránh tạo job trùng.

## 1) Endpoint map

- `POST /lab-assignments/{id}/grade` — tạo job chấm cho các submission chưa có job active
- `POST /lab-assignments/{id}/grade-all` — alias của `/grade`, nên dùng cho nút "Grade All"
- `POST /lab-submissions/{id}/regrade` — xếp một submission vào cuối hàng đợi chấm lại
- `POST /lab-submissions/regrade-all?assignmentId={id}` — xếp toàn bộ submission cần chấm lại vào hàng đợi
- `GET /lab-assignments/{id}/grading-progress` — poll tiến trình bài đang chấm hiện tại và queue
- `GET /lab-assignments/{id}/roster` — lấy bảng điểm tổng quan, dùng để render table chính
- `GET /lab-submissions/{id}/results` — lấy chi tiết testcase của một submission, chỉ gọi khi mở detail

## 2) Contracts

### Grade all

`POST /lab-assignments/{id}/grade-all`

Alias tương đương: `POST /lab-assignments/{id}/grade`

**Auth:** Không thay đổi so với nhóm `lab-assignments` hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab assignment id |

**Body:** none

**Response `data`:**

| Field | Type | Note |
|------|------|------|
| `jobsCreated` | `number` | số job mới được tạo |
| `message` | `string` | mô tả kết quả |

**Behavior:**

- Backend chỉ tạo job cho submission chưa có job `Pending` hoặc `Running`.
- Worker xử lý tuần tự: một bài hoàn tất, lưu kết quả, cleanup Docker xong rồi mới lấy bài kế tiếp.
- Nếu `jobsCreated = 0`, FE không cần gọi lại liên tục; kiểm tra progress/roster để biết có job active không.

---

### Regrade one

`POST /lab-submissions/{id}/regrade`

**Auth:** Không thay đổi so với nhóm `lab-submissions` hiện tại.

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab submission id |

**Body:** none

**Response `data`:**

| Field | Type | Note |
|------|------|------|
| `queued` | `boolean` | `true` nếu tạo job mới; `false` nếu submission đã có job `Pending` |
| `message` | `string` | mô tả kết quả |

**Behavior:**

- Job đang `Running` không bị hủy; regrade mới được xếp phía sau.
- Nếu `queued = false`, giữ UI ở trạng thái đang chờ và không spam request.
- Sau khi gọi thành công, poll `grading-progress` và refresh `roster`.

---

### Regrade all

`POST /lab-submissions/regrade-all?assignmentId={id}`

**Auth:** Không thay đổi so với nhóm `lab-submissions` hiện tại.

**Query params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `assignmentId` | `guid` | yes | Lab assignment id |

**Body:** none

**Response `data`:**

| Field | Type | Note |
|------|------|------|
| `queued` | `number` | số submission được tạo job regrade mới |

**Behavior:**

- Không hủy job đang chạy.
- Không tạo thêm job cho submission đã có job `Pending`.
- Dùng sau khi sửa testcase hoặc muốn chấm lại cả lớp; không cần xóa/upload lại submission.

---

### Grading progress

`GET /lab-assignments/{id}/grading-progress`

**Route params:**

| Param | Type | Required | Note |
|-------|------|----------|------|
| `id` | `guid` | yes | Lab assignment id |

**Response `data`:**

| Field | Type | Note |
|------|------|------|
| `assignmentId` | `guid` | lab assignment id |
| `assignmentStatus` | `string` | `Draft` \| `TestcasesReady` \| `Grading` \| `Done` |
| `runningSubmissionId` | `guid \| null` | submission đang chấm hoặc đang chờ đầu queue |
| `runningStudentCode` | `string \| null` | mã sinh viên tương ứng |
| `runningJobId` | `guid \| null` | job đang hiển thị progress |
| `runningJobStatus` | `string \| null` | `Pending` \| `Running` \| `Done` \| `Failed` |
| `runningPercent` | `number` | 0..100 theo số testcase đã có result |
| `executedTestCaseCount` | `number` | số testcase đã ghi result cho job hiện tại |
| `totalTestCaseCount` | `number` | tổng testcase `Approved` |
| `queuedSubmissionCount` | `number` | số job còn chờ phía sau |
| `completedSubmissionCount` | `number` | số submission có latest job `Done/Failed` |
| `isGradingActive` | `boolean` | `true` nếu còn job `Pending/Running` |

**Polling:**

- Poll mỗi `2s` khi `isGradingActive = true`.
- Dừng poll khi `isGradingActive = false`, sau đó refresh `roster` một lần.
- Khi `runningSubmissionId` đổi, FE nên chuyển highlight sang dòng sinh viên kế tiếp.

---

### Roster

`GET /lab-assignments/{id}/roster`

**Response `data`:** `LabAssignmentRosterItemDto[]`

| Field | Type | Note |
|------|------|------|
| `submissionId` | `guid` | id bài nộp |
| `studentCode` | `string` | mã sinh viên |
| `originalFileName` | `string` | tên file nộp |
| `submissionStatus` | `string` | `Pending` \| `Grading` \| `Done` \| `BuildFailed` \| `Error` |
| `latestJobId` | `guid \| null` | job mới nhất của submission |
| `jobStatus` | `string \| null` | `Pending` \| `Running` \| `Done` \| `Failed` |
| `totalScore` | `number \| null` | null nếu latest job chưa hoàn tất |
| `maxScore` | `number` | tổng điểm testcase `Approved` |
| `createdAt` | `string (ISO 8601)` | thời gian upload |
| `updatedAt` | `string (ISO 8601) \| null` | thời gian cập nhật |

**Usage:**

- Dùng endpoint này để render bảng chính.
- Không gọi `GET /lab-submissions/{id}/results` cho từng dòng.
- `totalScore = null` nghĩa là đang chờ/đang chấm, không phải 0 điểm.

## 3) Error codes

| HTTP | code | Message |
|------|------|---------|
| `404` | `NOT_FOUND` | `LabAssignment '{id}' not found.` |
| `404` | `NOT_FOUND` | `LabSubmission '{id}' not found.` |

## 4) Recommended FE flow

### Initial grade-all

1. Teacher approve testcase.
2. Teacher upload submissions.
3. FE gọi `GET /lab-assignments/{id}/roster` để render bảng.
4. Teacher bấm "Grade All".
5. FE gọi `POST /lab-assignments/{id}/grade-all`.
6. FE bắt đầu poll `GET /lab-assignments/{id}/grading-progress` mỗi `2s`.
7. Trong lúc active, refresh `roster` theo nhịp nhẹ hơn, ví dụ `4-5s`, hoặc khi `runningSubmissionId` đổi.
8. Khi `isGradingActive = false`, dừng poll và gọi `roster` lần cuối.

### Regrade one

1. User bấm "Regrade" ở một row.
2. FE gọi `POST /lab-submissions/{submissionId}/regrade`.
3. Nếu `queued = true`, hiển thị row là `Pending` và đưa vào queue.
4. Nếu `queued = false`, disable nút hoặc show "Already queued".
5. Poll `grading-progress` nếu chưa active.

### Regrade all

1. User bấm "Regrade All".
2. FE confirm vì thao tác này có thể chạy lâu.
3. FE gọi `POST /lab-submissions/regrade-all?assignmentId={id}`.
4. Nếu `queued > 0`, poll `grading-progress`.
5. Nếu `queued = 0`, refresh `roster`; có thể tất cả bài đã có pending job hoặc không có submission.

## 5) FE notes

- Ưu tiên dùng `grade-all` cho UI rõ nghĩa; `/grade` vẫn tương thích.
- Không gọi `grade-all` nhiều lần trong lúc `isGradingActive = true`; backend idempotent nhưng UI nên tránh request thừa.
- Không gọi `regrade-all` liên tục; nếu cần, chờ queue hiện tại xong hoặc ít nhất kiểm `queued = 0`.
- Không hủy job đang chạy ở FE. Backend đã đảm bảo job đang chạy hoàn tất trước khi job sau bắt đầu.
- Chỉ mở `GET /lab-submissions/{id}/results` khi user xem detail/modal của một submission.
- Với `BuildFailed` hoặc `Error`, dùng `regrade` cho từng bài sau khi đã sửa nguyên nhân.
- Trong table, row đang chấm nên dựa vào `runningSubmissionId` từ progress, không suy luận từ tất cả row status.
