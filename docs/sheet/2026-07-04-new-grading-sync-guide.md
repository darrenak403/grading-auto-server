# Hướng dẫn Đồng bộ Điểm dành cho Grading Tool (Schema mới)

> **Lưu ý**: Hướng dẫn này thay thế cho tài liệu cũ (`docs/api/resubmission-sync-tool.md` và `docs/api/grading-tool-class-name-update.md`). Hệ thống đã chuyển đổi sang mô hình ERD chuẩn hóa. Bảng `submissions` cũ được thay bằng `class_lab_submissions`, và bảng `resubmission_requests` cũ được thay bằng `resubmission_requests_v2`.

Hệ thống Next.js hiển thị điểm và Grading Tool (.NET / Python) chia sẻ chung dự án Supabase. Tài liệu này hướng dẫn cách cập nhật Grading Tool từ cơ chế **ghi đè điểm cũ** (UPSERT) sang cơ chế **lưu lịch sử theo từng lần chấm (attempt-based, append-only)**.

---

## 1. Tổng quan Luồng Xử lý Mới

Khi Grading Tool chấm xong một bài Lab cho một sinh viên, thay vì thực hiện `UPSERT` trực tiếp vào bảng `submissions` như trước, tool sẽ thực hiện các bước sau:

1. **Resolve IDs**: Tìm `class_student_id` và `class_lab_id` dựa trên mã sinh viên (`student_code`), tên lớp (`class_name`) và mã lab (`lab_code`).
2. **Kiểm tra Yêu cầu Chấm lại**: Tìm xem sinh viên có yêu cầu chấm lại (`resubmission_requests_v2`) nào đang ở trạng thái `approved` hay không.
3. **Xác định Loại Bài nộp (`item_type`)**:
   - Nếu có yêu cầu chấm lại được duyệt: loại bài nộp là `resubmit`.
   - Nếu không, so sánh thời gian nộp hiện tại với `deadline` của lab đó để xác định là `original` (đúng hạn) hay `late` (muộn).
4. **Gọi RPC ghi nhận lượt chấm**: Gọi RPC `create_class_lab_submission` trên Supabase để tự động sinh số thứ tự lần chấm (`attempt_no` tự tăng) và ghi nhận kết quả.
5. **Cập nhật trạng thái Yêu cầu Chấm lại**: Nếu đây là lượt chấm để hoàn thành yêu cầu chấm lại, cập nhật trạng thái yêu cầu đó sang `completed`.

---

## 2. Chi tiết các Bước Gọi API qua REST

Tất cả các API call dưới đây đều sử dụng chung thông tin cấu hình Supabase:
- **Base URL**: `https://{your-project-id}.supabase.co`
- **Headers bắt buộc** (để bypass RLS bảo mật bằng Service Role Key):
  ```http
  apikey: {SUPABASE_SERVICE_ROLE_KEY}
  Authorization: Bearer {SUPABASE_SERVICE_ROLE_KEY}
  Content-Type: application/json
  ```

### Bước 1: Tìm `class_student_id` (ID Lớp - Sinh viên)
Lấy `id` của bảng `class_students` bằng cách lọc theo MSSV và Tên Lớp. 
*Lưu ý: Chuyển MSSV và Tên Lớp sang chữ HOA trước khi so sánh.*

* **HTTP Method**: `GET`
* **URL**: `/rest/v1/class_students?select=id&students!inner(student_code)&classes!inner(name)&students.student_code=eq.{STUDENT_CODE}&classes.name=eq.{CLASS_NAME}`
* **Kết quả trả về**:
  ```json
  [
    {
      "id": "d2c8828e-5b12-47ef-a0d3-356a1b24bf41" // Đây chính là class_student_id
    }
  ]
  ```
  *(Nếu trả về mảng rỗng `[]`, nghĩa là sinh viên không thuộc lớp này hoặc thông tin sai).*

### Bước 2: Tìm `class_lab_id` (ID Lab được giao cho Lớp) và Hạn nộp (`deadline`)
Lấy `id` và `deadline` của bảng `class_labs` bằng cách lọc theo Mã Lab và Tên Lớp.
*Lưu ý: Chuyển Mã Lab và Tên Lớp sang chữ HOA trước khi so sánh.*

* **HTTP Method**: `GET`
* **URL**: `/rest/v1/class_labs?select=id,deadline&labs!inner(code)&classes!inner(name)&labs.code=eq.{LAB_CODE}&classes.name=eq.{CLASS_NAME}`
* **Kết quả trả về**:
  ```json
  [
    {
      "id": "e4f8841a-1d54-4a2e-89a1-7789cb44df22", // Đây chính là class_lab_id
      "deadline": "2026-07-10T23:59:59+07:00" // Có thể null nếu chưa cấu hình hạn nộp
    }
  ]
  ```

### Bước 3: Tìm Yêu cầu Chấm lại đang chờ (`approved`)
Tìm xem có yêu cầu chấm lại nào của sinh viên này cho bài lab này đã được duyệt mà chưa hoàn thành hay không:

* **HTTP Method**: `GET`
* **URL**: `/rest/v1/resubmission_requests_v2?select=id&class_student_id=eq.{class_student_id}&class_lab_id=eq.{class_lab_id}&status=eq.approved&created_submission_id=is.null&order=updated_at.desc&limit=1`
* **Kết quả trả về**:
  - Nếu có:
    ```json
    [
      {
        "id": "5fa9911b-4d43-41bb-b855-66778899aabb" // Đây là fulfills_request_id
      }
    ]
    ```
  - Nếu không có: Trả về `[]` (khi đó `fulfills_request_id` sẽ là `null`).

### Bước 4: Gọi RPC `create_class_lab_submission`
RPC này đảm bảo tính toán chính xác và tăng số lần chấm (`attempt_no`) nguyên tử (advisory-locked), tránh race condition.

* **HTTP Method**: `POST`
* **URL**: `/rest/v1/rpc/create_class_lab_submission`
* **Payload**:
  ```json
  {
    "p_class_student_id": "d2c8828e-5b12-47ef-a0d3-356a1b24bf41",
    "p_class_lab_id": "e4f8841a-1d54-4a2e-89a1-7789cb44df22",
    "p_item_type": "original", // 'original' | 'late' | 'resubmit'
    "p_source_url": "https://github.com/...", // Link nộp bài (nếu có) hoặc null
    "p_score": 8.5, // Số thực điểm số
    "p_details": { "passed": 17, "failed": 3, "tests": [...] }, // JSON chi tiết test case
    "p_fulfills_request_id": null // UUID của request ở Bước 3, hoặc null nếu không phải resubmit
  }
  ```
  > [!IMPORTANT]
  > **Quy tắc xác định `p_item_type`**:
  > - Nếu tìm thấy approved request ở Bước 3: `p_item_type` bắt buộc là `"resubmit"` và truyền ID của request đó vào `p_fulfills_request_id`.
  > - Nếu không có request: So sánh thời gian chấm hiện tại với `deadline` nhận từ Bước 2. Nếu đã quá hạn, `p_item_type` là `"late"`, ngược lại là `"original"`.

* **Kết quả trả về**: Một object chứa thông tin dòng vừa tạo trong bảng `class_lab_submissions`.

### Bước 5: Cập nhật trạng thái Yêu cầu Chấm lại thành `completed` (nếu có)
Nếu ở Bước 3 tìm thấy request và đã truyền vào RPC ở Bước 4, sau khi RPC thực thi thành công, bạn cần chuyển trạng thái của request đó sang `completed` để học sinh có thể gửi yêu cầu mới ở các lần sau.

* **HTTP Method**: `PATCH`
* **URL**: `/rest/v1/resubmission_requests_v2?id=eq.{request_id}&status=eq.approved`
* **Payload**:
  ```json
  {
    "status": "completed",
    "completed_at": "2026-07-05T01:28:15Z", // Thời gian hiện tại dạng ISO 8601
    "updated_at": "2026-07-05T01:28:15Z"
  }
  ```

---

## 3. Ví dụ Code Minh Họa

### C# (Dành cho .NET Backend của `be` tool)

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class SupabaseSyncService
{
    private readonly HttpClient _client;
    private const string SupabaseUrl = "https://your-project-id.supabase.co";
    private const string ServiceRoleKey = "your-service-role-key";

    public SupabaseSyncService()
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("apikey", ServiceRoleKey);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ServiceRoleKey);
    }

    public async Task SyncGradeAsync(string studentCode, string className, string labCode, double score, object details, string sourceUrl)
    {
        studentCode = studentCode.Trim().ToUpper();
        className = className.Trim().ToUpper();
        labCode = labCode.Trim().ToUpper();

        // 1. Resolve class_student_id
        string classStudentId = await GetClassStudentIdAsync(studentCode, className);
        if (string.IsNullOrEmpty(classStudentId))
        {
            Console.WriteLine($"[Lỗi] Không tìm thấy thông tin sinh viên {studentCode} trong lớp {className}");
            return;
        }

        // 2. Resolve class_lab_id and deadline
        var (classLabId, deadlineStr) = await GetClassLabIdAndDeadlineAsync(labCode, className);
        if (string.IsNullOrEmpty(classLabId))
        {
            Console.WriteLine($"[Lỗi] Không tìm thấy bài Lab {labCode} được giao cho lớp {className}");
            return;
        }

        // 3. Check for approved resubmission request
        string approvedRequestId = await GetApprovedRequestIdAsync(classStudentId, classLabId);

        // 4. Determine item_type
        string itemType = "original";
        string fulfillsRequestId = null;

        if (!string.IsNullOrEmpty(approvedRequestId))
        {
            itemType = "resubmit";
            fulfillsRequestId = approvedRequestId;
        }
        else if (!string.IsNullOrEmpty(deadlineStr) && DateTime.TryParse(deadlineStr, out DateTime deadline))
        {
            if (DateTime.UtcNow > deadline.ToUniversalTime())
            {
                itemType = "late";
            }
        }

        // 5. Call RPC to insert submission
        bool isSuccess = await CreateSubmissionRpcAsync(classStudentId, classLabId, itemType, score, details, sourceUrl, fulfillsRequestId);

        // 6. Complete resubmission request if applicable
        if (isSuccess && !string.IsNullOrEmpty(approvedRequestId))
        {
            await CompleteResubmissionRequestAsync(approvedRequestId);
        }
    }

    private async Task<string> GetClassStudentIdAsync(string studentCode, string className)
    {
        var url = $"{SupabaseUrl}/rest/v1/class_students?select=id&students!inner(student_code)&classes!inner(name)&students.student_code=eq.{studentCode}&classes.name=eq.{className}";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetArrayLength() > 0 ? doc.RootElement[0].GetProperty("id").GetString() : null;
    }

    private async Task<(string id, string deadline)> GetClassLabIdAndDeadlineAsync(string labCode, string className)
    {
        var url = $"{SupabaseUrl}/rest/v1/class_labs?select=id,deadline&labs!inner(code)&classes!inner(name)&labs.code=eq.{labCode}&classes.name=eq.{className}";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.GetArrayLength() == 0) return (null, null);
        
        var element = doc.RootElement[0];
        string id = element.GetProperty("id").GetString();
        string deadline = element.TryGetProperty("deadline", out var dProp) && dProp.ValueKind != JsonValueKind.Null ? dProp.GetString() : null;
        return (id, deadline);
    }

    private async Task<string> GetApprovedRequestIdAsync(string classStudentId, string classLabId)
    {
        var url = $"{SupabaseUrl}/rest/v1/resubmission_requests_v2?select=id&class_student_id=eq.{classStudentId}&class_lab_id=eq.{classLabId}&status=eq.approved&created_submission_id=is.null&order=updated_at.desc&limit=1";
        var response = await _client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetArrayLength() > 0 ? doc.RootElement[0].GetProperty("id").GetString() : null;
    }

    private async Task<bool> CreateSubmissionRpcAsync(string classStudentId, string classLabId, string itemType, double score, object details, string sourceUrl, string fulfillsRequestId)
    {
        var payload = new
        {
            p_class_student_id = classStudentId,
            p_class_lab_id = classLabId,
            p_item_type = itemType,
            p_source_url = sourceUrl,
            p_score = score,
            p_details = details,
            p_fulfills_request_id = fulfillsRequestId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _client.PostAsync($"{SupabaseUrl}/rest/v1/rpc/create_class_lab_submission", content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Lỗi] RPC thất bại: {errText}");
            return false;
        }
        return true;
    }

    private async Task CompleteResubmissionRequestAsync(string requestId)
    {
        var payload = new
        {
            status = "completed",
            completed_at = DateTime.UtcNow.ToString("o"),
            updated_at = DateTime.UtcNow.ToString("o")
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _client.PatchAsync($"{SupabaseUrl}/rest/v1/resubmission_requests_v2?id=eq.{requestId}&status=eq.approved", content);
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine($"[Sync] Đã cập nhật trạng thái Yêu cầu chấm lại {requestId} thành 'completed'.");
        }
        else
        {
            var errText = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[Cảnh báo] Không thể update trạng thái request {requestId}: {errText}");
        }
    }
}
```

---

### Python (Dành cho local script chạy nhanh)

```python
import requests
from datetime import datetime, timezone

SUPABASE_URL = "https://your-project-id.supabase.co"
SUPABASE_KEY = "your-service-role-key"

HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json",
}

def sync_grade(student_code, class_name, lab_code, score, details, source_url=None):
    student_code = student_code.strip().upper()
    class_name = class_name.strip().upper()
    lab_code = lab_code.strip().upper()

    # 1. Resolve class_student_id
    url_student = f"{SUPABASE_URL}/rest/v1/class_students?select=id&students!inner(student_code)&classes!inner(name)&students.student_code=eq.{student_code}&classes.name=eq.{class_name}"
    res_student = requests.get(url_student, headers=HEADERS)
    res_student.raise_for_status()
    students_data = res_student.json()
    if not students_data:
        print(f"Lỗi: Không tìm thấy sinh viên {student_code} trong lớp {class_name}")
        return
    class_student_id = students_data[0]["id"]

    # 2. Resolve class_lab_id & deadline
    url_lab = f"{SUPABASE_URL}/rest/v1/class_labs?select=id,deadline&labs!inner(code)&classes!inner(name)&labs.code=eq.{lab_code}&classes.name=eq.{class_name}"
    res_lab = requests.get(url_lab, headers=HEADERS)
    res_lab.raise_for_status()
    labs_data = res_lab.json()
    if not labs_data:
        print(f"Lỗi: Không tìm thấy lab {lab_code} cho lớp {class_name}")
        return
    class_lab_id = labs_data[0]["id"]
    deadline_str = labs_data[0].get("deadline")

    # 3. Check approved resubmissions
    url_req = f"{SUPABASE_URL}/rest/v1/resubmission_requests_v2?select=id&class_student_id=eq.{class_student_id}&class_lab_id=eq.{class_lab_id}&status=eq.approved&created_submission_id=is.null&order=updated_at.desc&limit=1"
    res_req = requests.get(url_req, headers=HEADERS)
    res_req.raise_for_status()
    req_data = res_req.json()

    # 4. Set item_type & fulfills_request_id
    item_type = "original"
    fulfills_request_id = None

    if req_data:
        item_type = "resubmit"
        fulfills_request_id = req_data[0]["id"]
    elif deadline_str:
        deadline = datetime.fromisoformat(deadline_str.replace("Z", "+00:00"))
        if datetime.now(timezone.utc) > deadline:
            item_type = "late"

    # 5. Call RPC create submission
    payload_rpc = {
        "p_class_student_id": class_student_id,
        "p_class_lab_id": class_lab_id,
        "p_item_type": item_type,
        "p_source_url": source_url,
        "p_score": score,
        "p_details": details,
        "p_fulfills_request_id": fulfills_request_id
    }
    
    url_rpc = f"{SUPABASE_URL}/rest/v1/rpc/create_class_lab_submission"
    res_rpc = requests.post(url_rpc, headers=HEADERS, json=payload_rpc)
    if res_rpc.status_code not in [200, 201]:
        print(f"Lỗi khi lưu submission: {res_rpc.text}")
        return

    # 6. Complete request if resubmitted
    if fulfills_request_id:
        url_patch = f"{SUPABASE_URL}/rest/v1/resubmission_requests_v2?id=eq.{fulfills_request_id}&status=eq.approved"
        payload_patch = {
            "status": "completed",
            "completed_at": datetime.now(timezone.utc).isoformat(),
            "updated_at": datetime.now(timezone.utc).isoformat(),
        }
        res_patch = requests.patch(url_patch, headers=HEADERS, json=payload_patch)
        if res_patch.status_code in [200, 204]:
            print(f"Đã sync điểm và hoàn thành yêu cầu chấm lại {fulfills_request_id} cho {student_code}")
        else:
            print(f"Cảnh báo: Lỗi khi update trạng thái request: {res_patch.text}")
    else:
        print(f"Đã sync điểm thành công cho {student_code} ({item_type})")
```
