# FE Handoff: Supabase Class Lab Dropdown Sync

> Branch: `tvu`
> Date: 2026-07-05

## 1) Endpoint map

- `GET /api/v1/lab-assignments/supabase-dropdown-options` — lấy danh sách term, class và lab từ Supabase để hiển thị dropdown sync theo thứ tự `term -> class -> lab`.
- `POST /api/v1/lab-assignments/{id}/sync-supabase` — đồng bộ điểm lab assignment lên Supabase theo schema mới `class_lab_submissions`.
- `POST /api/v1/lab-assignments/sync-supabase-grade` — đồng bộ trực tiếp một kết quả chấm theo payload v2 từ grading tool.
- `POST /api/v1/lab-assignments/sync-supabase-grades` — đồng bộ trực tiếp toàn bộ kết quả trong một session chấm.

## 2) Contracts

### Supabase dropdown options

`GET /api/v1/lab-assignments/supabase-dropdown-options`

**Auth:** Public / theo cấu hình API hiện tại.

**Query params:**
| Param | Type | Default | Note |
|-------|------|---------|------|
| `termId` | `string` | `null` | Optional. Nếu truyền, API chỉ trả classes/labs thuộc term này. |
| `className` | `string` | `null` | Optional. Chỉ nên truyền sau khi đã chọn `termId`. Nếu truyền, API chỉ trả labs được gán cho class này trong term đã chọn. Giá trị được trim và uppercase trước khi query Supabase. |

**Body:** Không có.

**Response `data`:**
- `terms` — `Array<{ id: string, code: null, name: string | null }>` danh sách term từ Supabase `terms`; schema hiện chỉ dùng `id` và `name`.
- `classes` — `Array<{ name: string, termId: string | null, termCode: string | null, termName: string | null }>` danh sách class từ Supabase `classes`, có thể lọc theo `termId`.
- `labs` — `Array<{ code: string, title: null, className: string | null, termId: string | null, termCode: string | null, termName: string | null, deadline: string | null }>` danh sách lab assignment từ Supabase `class_labs` join `labs`, `classes`, `terms`.

Example:
```json
{
  "terms": [
    {
      "id": "11111111-1111-1111-1111-111111111111",
      "code": null,
      "name": "Summer 2026"
    }
  ],
  "classes": [
    {
      "name": "SE1815",
      "termId": "11111111-1111-1111-1111-111111111111",
      "termCode": null,
      "termName": "Summer 2026"
    }
  ],
  "labs": [
    {
      "code": "LAB1",
      "title": null,
      "className": "SE1815",
      "termId": "11111111-1111-1111-1111-111111111111",
      "termCode": null,
      "termName": "Summer 2026",
      "deadline": "2026-07-10T23:59:59+07:00"
    }
  ]
}
```

### Sync lab assignment to Supabase

`POST /api/v1/lab-assignments/{id}/sync-supabase`

**Auth:** Public / theo cấu hình API hiện tại.

**Route params:**
| Param | Type | Default | Note |
|-------|------|---------|------|
| `id` | `Guid` | Required | ID của local lab assignment cần sync. |

**Query params:** Không có.

**Body:**
```json
{
  "labId": "LAB1",
  "className": "SE1815",
  "termId": "11111111-1111-1111-1111-111111111111"
}
```

Validation rules:
- `labId` optional. Nếu không truyền, backend dùng `LabAssignment.Title`, trim và uppercase để resolve `labs.code`.
- `className` optional. Nếu không truyền, backend thử resolve từ Supabase roster; nếu không resolve được thì submission đó sync fail và được log.
- `termId` optional về mặt API để giữ tương thích, nhưng FE nên luôn gửi. Nếu class name/lab code bị trùng giữa nhiều term, không gửi `termId` có thể resolve sai hoặc không ổn định.
- Sync hiện dùng Supabase schema mới: resolve `class_students`, resolve `class_labs`, kiểm tra `resubmission_requests_v2`, gọi RPC `create_class_lab_submission`.

**Response `data`:**
- `syncedCount` — `number`, số submissions sync thành công.
- `message` — `string`, thông báo tổng kết.

Example:
```json
{
  "syncedCount": 5,
  "message": "Successfully synced 5 submissions to Supabase."
}
```

## 3) Error codes

| HTTP | code | message |
|------|------|---------|
| 404 | `NOT_FOUND` | `LabAssignment '{id}' not found.` |
| 500 | `INTERNAL_SERVER_ERROR` | Supabase chưa cấu hình URL/key hoặc Supabase REST/RPC lỗi. |
| 500 | `INTERNAL_SERVER_ERROR` | Dropdown endpoint lỗi nếu không query được `terms`, `classes` hoặc `class_labs` từ Supabase. |

## 4) FE notes

- FE nên gọi `GET /supabase-dropdown-options` lúc mở form sync để load term dropdown.
- Khi user chọn term, gọi lại `GET /supabase-dropdown-options?termId={termId}` để lấy classes thuộc term.
- Khi user chọn class, gọi lại `GET /supabase-dropdown-options?termId={termId}&className={className}` để lấy labs tương ứng.
- Field `labs.title` hiện luôn `null` vì schema guide chỉ đảm bảo cột `labs.code`; dùng `code` làm label.
- Field `termCode` hiện là `null` vì Supabase `terms` đang không có cột `code`; dùng `termName` làm label.
- Với sync, nên gửi đủ `termId`, `className` và `labId` từ dropdown để tránh backend resolve nhầm term/class/lab.
- Sync là append-only theo attempt mới, không còn upsert đè bảng `submissions` cũ.

### Sync single grade payload v2

`POST /api/v1/lab-assignments/sync-supabase-grade`

**Auth:** Public / theo cấu hình API hiện tại.

**Body:**
```json
{
  "termId": "11111111-1111-1111-1111-111111111111",
  "studentCode": "SE181234",
  "className": "SE1812",
  "labCode": "LAB1",
  "score": 8.5,
  "details": {
    "passed": 17,
    "failed": 3,
    "tests": []
  },
  "sourceUrl": "https://github.com/example/repo"
}
```

Validation rules:
- `studentCode`, `className`, `labCode` required.
- Backend trim + uppercase `studentCode`, `className`, `labCode` before resolving Supabase IDs.
- `score` is the final numeric score for this grading result.
- `details` is forwarded to RPC `p_details` as JSON.
- `sourceUrl` optional, forwarded to RPC `p_source_url`.
- `termId` optional về mặt API, nhưng FE nên gửi từ dropdown term để backend resolve đúng `class_students` và `class_labs` khi class/lab trùng giữa nhiều term.

**Response `data`:**
- `classStudentId` — `string`, resolved Supabase `class_students.id`.
- `classLabId` — `string`, resolved Supabase `class_labs.id`.
- `itemType` — `"original" | "late" | "resubmit"`.
- `fulfillsRequestId` — `string | null`, approved resubmission request completed by this sync.

### Sync grading session payload v2

`POST /api/v1/lab-assignments/sync-supabase-grades`

**Auth:** Public / theo cấu hình API hiện tại.

**Body:**
```json
{
  "termId": "11111111-1111-1111-1111-111111111111",
  "className": "SE1812",
  "labCode": "LAB1",
  "submissions": [
    {
      "studentCode": "SE161039",
      "score": 0,
      "details": {
        "passed": 0,
        "failed": 12,
        "tests": []
      },
      "sourceUrl": "https://github.com/example/repo"
    }
  ]
}
```

Validation rules:
- `className`, `labCode`, `submissions` required.
- `termId` optional về mặt API, nhưng FE nên gửi từ dropdown term.
- `submissions` must contain at least one item.
- Each item requires `studentCode` and `details`.
- Backend continues syncing remaining items if one item fails.

**Response `data`:**
- `total` — `number`, number of requested submissions.
- `syncedCount` — `number`, successfully synced submissions.
- `failedCount` — `number`, failed submissions.
- `synced` — `Array<{ studentCode, classStudentId, classLabId, itemType, fulfillsRequestId }>`
- `failed` — `Array<{ studentCode, error }>`
