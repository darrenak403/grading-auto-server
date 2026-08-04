# Resubmission Sync Tool Contract

Tài liệu này dành cho tool/local grader khi sync kết quả chấm lên Supabase.

## Rule bắt buộc

Khi sync kết quả cho một `student_id + lab_id`, tool phải kiểm tra bảng `resubmission_requests` trước khi ghi điểm:

1. Nếu có request `status = 'approved'` mới nhất cho đúng `student_id + lab_id`, kết quả sync được xem là kết quả của lần resubmit đã được duyệt.
2. Tool vẫn upsert vào bảng `submissions` theo khóa hiện có của hệ thống, tức là ghi đè kết quả cũ của sinh viên cho lab đó.
3. Sau khi upsert `submissions` thành công, tool phải update request approved đó thành:
   - `status = 'completed'`
   - `completed_at = now()`
   - `updated_at = now()`
4. Nếu upsert `submissions` thất bại, không được chuyển request sang `completed`.
5. Nếu không có request `approved`, tool sync điểm bình thường và không thay đổi bảng `resubmission_requests`.

## Supabase REST flow

### 1. Tìm approved request

```http
GET /rest/v1/resubmission_requests?student_id=eq.{student_id}&lab_id=eq.{lab_id}&status=eq.approved&order=updated_at.desc&limit=1
```

Headers:

```http
apikey: {SUPABASE_SERVICE_ROLE_KEY}
Authorization: Bearer {SUPABASE_SERVICE_ROLE_KEY}
Content-Type: application/json
```

### 2. Upsert kết quả vào submissions

```http
POST /rest/v1/submissions
Prefer: resolution=merge-duplicates
```

Payload tối thiểu:

```json
{
  "student_id": "SE180123",
  "lab_id": "Lab2",
  "class_name": "SE1815",
  "score": 8.5,
  "status": "Done",
  "details": {}
}
```

### 3. Mark request completed sau khi upsert thành công

```http
PATCH /rest/v1/resubmission_requests?id=eq.{request_id}&status=eq.approved
```

Payload:

```json
{
  "status": "completed",
  "completed_at": "2026-07-01T00:00:00.000Z",
  "updated_at": "2026-07-01T00:00:00.000Z"
}
```

Điều kiện `status=eq.approved` là guard chống update nhầm request đã bị đổi trạng thái.

## State workflow

```text
pending -> approved -> completed -> student can request again
pending -> rejected -> student can request again
```

Trong lúc request đang `approved`, student UI và API sẽ chặn tạo request mới cho cùng lab.
