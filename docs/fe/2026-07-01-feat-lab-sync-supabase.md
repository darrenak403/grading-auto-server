# FE Handoff: Đồng bộ điểm số bài Lab lên Supabase

> Branch: `tvu`
> Date: 2026-07-01

## 1) Endpoint map

- `POST /api/v1/lab-assignments/{id}/sync-supabase` — Đồng bộ thủ công toàn bộ kết quả chấm bài của Lab Assignment tương ứng lên Supabase.

---

## 2) Contracts

### Đồng bộ điểm số bài Lab lên Supabase

`POST /api/v1/lab-assignments/{id}/sync-supabase`

**Auth:** Any authenticated user / Public (theo cấu hình hiện tại của API)

**Route params:**
| Param | Type | Default | Note |
|-------|------|---------|------|
| `id` | `Guid` | _None_ | ID của Lab Assignment cần đồng bộ. |

**Query params:** Không có

**Body:** Không có (Empty Body)

**Response `data`:**
```json
{
  "success": true,
  "message": "Successfully synced 5 submissions to Supabase.",
  "data": {
    "syncedCount": 5,
    "message": "Successfully synced 5 submissions to Supabase."
  },
  "errors": null,
  "traceId": "0HN0..."
}
```
* `syncedCount` — số lượng bài nộp (submissions) đã được đồng bộ lên Supabase thành công.
* `message` — thông báo chi tiết kết quả.

---

## 3) Error codes

| HTTP | code | message |
|------|------|---------|
| 404 | NOT_FOUND | LabAssignment '{id}' not found. |
| 500 | INTERNAL_SERVER_ERROR | Lỗi hệ thống hoặc lỗi kết nối đến Supabase REST API. |

---

## 4) FE notes

* **Cách Đồng bộ hiện tại**: Hệ thống chỉ đồng bộ lên Supabase khi giảng viên gọi endpoint sync thủ công. Worker không còn tự động đẩy điểm sau khi chấm xong.
* **Thời gian phản hồi**: API này gửi HTTP requests trực tiếp đến Supabase cho từng sinh viên để cập nhật điểm số. Với các lớp học đông sinh viên, API này có thể mất vài giây để hoàn thành. Cần hiển thị loading spinner hoặc chế độ chờ trên UI phù hợp.
* **Định dạng dữ liệu trên Supabase**:
  * Điểm số (`score`) được tính tổng tự động từ các test case và hỗ trợ điểm override thủ công.
  * Chi tiết test case (`details` kiểu `jsonb`) chứa số lượng test case pass/fail và danh sách chi tiết (tên test case dạng `[METHOD] URL - Description`, trạng thái pass/fail, điểm đạt được, max score, và error message nếu fail).
