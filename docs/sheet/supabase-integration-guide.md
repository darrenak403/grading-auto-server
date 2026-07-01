# Hướng dẫn Tích hợp Supabase làm Database hiển thị điểm PRN232

Tài liệu này hướng dẫn cách chuyển đổi cơ sở dữ liệu từ Google Sheets sang **Supabase (PostgreSQL)** để lưu trữ điểm số theo đợt/lab/lớp từ máy local và hiển thị realtime lên ứng dụng Next.js deploy trực tuyến.

---

## 1. Khởi Tạo Dự Án Supabase

1. Truy cập vào [Supabase](https://supabase.com/) và đăng nhập (hoặc tạo tài khoản miễn phí).
2. Bấm **New Project** (Dự án mới) -> Chọn tổ chức -> Đặt tên dự án (ví dụ: `prn232-auto-grading`).
3. Đặt mật khẩu database (Database Password) và chọn khu vực (Region) gần bạn nhất (ví dụ: `Singapore`).
4. Chờ 1-2 phút để dự án khởi tạo hoàn tất.

---

## 2. Thiết Kế Cơ Sở Dữ Liệu (SQL Schema)

Vào mục **SQL Editor** ở thanh menu bên trái dự án Supabase của bạn, bấm **New query** và chạy đoạn lệnh SQL sau để tạo các bảng dữ liệu:

```sql
-- 1. Tạo bảng danh sách sinh viên được phép đăng nhập (Whitelist)
create table allowed_emails (
  email text primary key,
  student_id text not null,
  class_name text not null
);

-- 2. Tạo bảng danh sách thông tin sinh viên đã từng đăng nhập
create table students (
  email text primary key references allowed_emails(email) on delete cascade,
  name text not null,
  student_id text not null,
  class_name text not null,
  created_at timestamp with time zone default timezone('utc'::text, now()) not null
);

-- 3. Tạo bảng lưu trữ điểm số các bài Lab (submissions)
create table submissions (
  student_id text not null,
  lab_id text not null,
  class_name text not null,
  score numeric(4,2) not null,
  status text not null,
  details jsonb, -- Lưu chi tiết test case pass/fail dạng JSON
  updated_at timestamp with time zone default timezone('utc'::text, now()) not null,
  
  -- Khóa chính kép (student_id + lab_id) phục vụ cho cơ chế UPSERT (ghi đè khi chấm lại)
  primary key (student_id, lab_id)
);
```

---

## 3. Cấu Hình Bảo Mật (Next.js Server-side Authorization)

Để tránh sinh viên xem trộm điểm của người khác, chúng ta sẽ thực hiện xác thực an toàn tại **Next.js Server-side**. 
Next.js sẽ nhận diện email của sinh viên từ Token đăng nhập (đã giải mã), sau đó sử dụng Supabase Client với quyền `service_role` để truy xuất đúng dữ liệu điểm của MSSV đó.

---

## 4. Script Python Đẩy Điểm Hàng Loạt từ Local (Local Grader)

Sau khi hệ thống local của bạn chạy chấm bài xong cho một lớp (ví dụ lớp `SE1815` bài `Lab1`), kết quả lưu dưới dạng JSON ở local. Bạn hãy chạy script Python này ở local để đồng bộ điểm lên Supabase (Tự động chèn mới hoặc ghi đè điểm cũ nếu chấm lại):

### Cài đặt thư viện ở local:
```bash
pip install requests
```

### Script `upload_grades.py`:
```python
import json
import requests

# Cấu hình Supabase của bạn (Lấy ở Project Settings -> API trên Supabase)
SUPABASE_URL = "https://your-project-id.supabase.co"
SUPABASE_KEY = "your-service-role-key"  # Dùng service_role key để bypass RLS ghi dữ liệu

HEADERS = {
    "apikey": SUPABASE_KEY,
    "Authorization": f"Bearer {SUPABASE_KEY}",
    "Content-Type": "application/json",
    "Prefer": "resolution=merge-duplicates"  # Kích hoạt tính năng UPSERT (ghi đè nếu trùng)
}

def upload_class_grades(json_file_path, class_name, lab_id):
    # Đọc dữ liệu điểm chấm local
    with open(json_file_path, "r", encoding="utf-8") as f:
        results = json.load(f)
        
    payload = []
    for item in results:
        payload.append({
            "student_id": item["MSSV"],
            "lab_id": lab_id,
            "class_name": class_name,
            "score": item["Score"],
            "status": item["Status"],
            "details": item.get("TestDetails", {}) # JSON chứa chi tiết pass/fail
        })
        
    # Gửi POST request dạng UPSERT lên bảng submissions
    url = f"{SUPABASE_URL}/rest/v1/submissions"
    response = requests.post(url, headers=HEADERS, json=payload)
    
    if response.status_code in [200, 201]:
        print(f"Đã cập nhật bảng điểm lớp {class_name} - Bài {lab_id} thành công!")
    else:
        print(f"Lỗi khi upload điểm: {response.text}")

# Ví dụ chạy thực thi đẩy file điểm
# upload_class_grades("SE1815_Lab1.json", "SE1815", "Lab1")
```

---

## 5. Tích Hợp Vào Ứng Dụng Next.js (Xem Điểm)

### Bước 1: Cài đặt SDK Supabase vào dự án
```bash
npm install @supabase/supabase-js
```

### Bước 2: Thêm các biến môi trường vào `.env.local`
```env
NEXT_PUBLIC_SUPABASE_URL=https://your-project-id.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-public-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key
```

### Bước 3: Tạo File Client Helper (`lib/supabase.ts`)
```typescript
import { createClient } from '@supabase/supabase-js';

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL || '';
const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY || '';

export const supabase = createClient(supabaseUrl, supabaseAnonKey);
```

### Bước 4: Viết API Route lấy điểm an toàn (`app/api/grades/route.ts`)
API này giải mã JWT token từ cookie đăng nhập, lấy MSSV và gọi Supabase để truy xuất điểm, bảo mật không lo lộ dữ liệu:

```typescript
import { NextResponse } from 'next/server';
import { cookies } from 'next/headers';
import { jwtDecode } from 'jwt-decode';
import { createClient } from '@supabase/supabase-js';

const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL || '';
const supabaseServiceKey = process.env.SUPABASE_SERVICE_ROLE_KEY || '';

// Sử dụng Client có quyền Service Role ở phía Server-side để truy cập DB an toàn
const supabaseServer = createClient(supabaseUrl, supabaseServiceKey);

interface UserPayload {
  studentId?: string;
  role: string;
}

export async function GET() {
  try {
    const cookieStore = await cookies();
    const token = cookieStore.get('authToken')?.value;

    if (!token) {
      return NextResponse.json({ success: false, error: 'Unauthorized' }, { status: 401 });
    }

    const decoded = jwtDecode<UserPayload>(token);
    const studentId = decoded.studentId;

    if (!studentId) {
      return NextResponse.json({ success: false, error: 'Student ID not found in token' }, { status: 400 });
    }

    // Truy vấn dữ liệu điểm của đúng sinh viên này từ Supabase
    const { data, error } = await supabaseServer
      .from('submissions')
      .select('*')
      .eq('student_id', studentId)
      .order('lab_id', { ascending: true });

    if (error) {
      return NextResponse.json({ success: false, error: error.message }, { status: 500 });
    }

    return NextResponse.json({ success: true, data });
  } catch (err) {
    return NextResponse.json({ success: false, error: 'Internal Server Error' }, { status: 500 });
  }
}
```
