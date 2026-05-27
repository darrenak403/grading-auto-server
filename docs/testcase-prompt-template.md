# Hướng dẫn tạo Test Case từ file đề bài Lab

## Ý tưởng

Paste file đề bài `.md` của lab + prompt bên dưới vào **ChatGPT** hoặc **Gemini**.
AI trả về mảng JSON → paste thẳng vào API batch import.

```
POST /api/lab-assignments/{id}/testcases/batch
Content-Type: application/json

[ ...mảng JSON AI trả về... ]
```

---

## Cấu trúc một test case

```json
{
  "httpMethod": "GET",
  "urlTemplate": "/api/semesters/1",
  "description": "Get semester by ID",
  "inputJson": null,
  "expectJson": "{\"success\":true,\"data\":{\"semesterId\":1}}",
  "expectedStatusCode": 200,
  "matchMode": "Subset",
  "score": 1.0
}
```

| Field                | Mô tả                                                                                                             |
| -------------------- | ----------------------------------------------------------------------------------------------------------------- |
| `httpMethod`         | `GET` `POST` `PUT` `DELETE` `PATCH` hoặc `SOURCE` (kiểm tra source code)                                          |
| `urlTemplate`        | URL tương đối. Dùng ID cụ thể từ seed data (ví dụ `/api/semesters/1`) hoặc `{id}` placeholder                     |
| `description`        | Nhãn hiển thị trong kết quả chấm                                                                                  |
| `inputJson`          | POST/PUT: request body dạng JSON string. GET: query params dạng JSON string `"{\"page\":1}"`. `null` nếu không có |
| `expectJson`         | Phần response cần kiểm tra dạng JSON string. `null` nếu dùng `StatusOnly`                                         |
| `expectedStatusCode` | `200` `201` `204` `400` `404`                                                                                     |
| `matchMode`          | `Subset` (mặc định) — `Exact` — `StatusOnly`                                                                      |
| `score`              | Điểm của test case này                                                                                            |

**matchMode nhanh:**

- `Subset` → response **chứa** các field trong expectJson (dùng hầu hết)
- `StatusOnly` → chỉ check HTTP status, bỏ qua body (dùng cho DELETE 204, 404, 400, và tất cả `SOURCE`)
- `Exact` → response phải khớp chính xác từng field (hiếm dùng)

---

## SOURCE test case — kiểm tra kiến trúc / cấu trúc code

Khi `httpMethod = "SOURCE"`, worker **không gọi HTTP** mà **scan source code** trong file ZIP của sinh viên.
`urlTemplate` chứa rule theo format `loại:tham-số`.

| Rule                | Ví dụ urlTemplate                                 | Ý nghĩa                            |
| ------------------- | ------------------------------------------------- | ---------------------------------- |
| `project-name`      | `project-name:PRN232.*.API`                       | Có file `.csproj` tên khớp pattern |
| `project-count`     | `project-count:3`                                 | Đúng 3 file `.csproj` (3 layer)    |
| `folder-exists`     | `folder-exists:**/Controllers`                    | Có thư mục Controllers             |
| `file-exists`       | `file-exists:**/docker-compose.yml`               | Có file docker-compose.yml         |
| `file-contains`     | `file-contains:**/Services/*.cs:interface`        | File service dùng interface        |
| `file-not-contains` | `file-not-contains:**/Controllers/*.cs:DbContext` | Controller không gọi thẳng DB      |

**Đặc điểm SOURCE test case:**

- Luôn chạy **trước** Docker — ngay sau khi giải nén ZIP
- Chạy **dù Docker build thất bại** — sinh viên vẫn nhận điểm kiến trúc
- `expectedStatusCode = 200`, `matchMode = "StatusOnly"`, `expectJson = null`

**Ví dụ JSON cho Lab 1 (3-layer architecture):**

```json
[
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-count:3",
    "description": "Solution has 3 projects (3-layer architecture)",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 2.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-name:PRN232.*.API",
    "description": "API project follows PRN232.[Name].API naming convention",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-name:PRN232.*.Services",
    "description": "Service project follows PRN232.[Name].Services naming convention",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-name:PRN232.*.Repositories",
    "description": "Repository project follows PRN232.[Name].Repositories naming convention",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "folder-exists:**/Controllers",
    "description": "Controllers folder exists in API project",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 0.5
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-exists:**/docker-compose.yml",
    "description": "docker-compose.yml included in submission",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-not-contains:**/Controllers/*.cs:DbContext",
    "description": "Controllers do not directly reference DbContext (no business logic in controller)",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 2.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-not-contains:**/Controllers/*.cs:Repository",
    "description": "Controllers do not directly reference Repository classes",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  }
]
```

---

## Prompt paste vào ChatGPT / Gemini

> **Bước 1:** Mở ChatGPT hoặc Gemini  
> **Bước 2:** Paste đoạn prompt bên dưới  
> **Bước 3:** Paste tiếp **toàn bộ nội dung file đề bài `.md`** vào ngay sau dòng `[LAB SPEC]`  
> **Bước 4:** Gửi → AI trả về mảng JSON → copy và import vào API batch endpoint

---

````
You are a test case generator for an automated REST API grading system.
Read the lab spec below and return ONLY a valid JSON array. No explanation, no markdown, no ```json``` wrapper.
Output must start with [ and end with ] and be directly parseable by JSON.parse().

=== FIELD SCHEMA ===

{
  "httpMethod": "SOURCE" | "GET" | "POST" | "PUT" | "DELETE",
  "urlTemplate": "<relative URL or SOURCE rule>",
  "description": "<short English label>",
  "inputJson": "<JSON escaped as string>" | null,
  "expectJson": "<JSON escaped as string>" | null,
  "expectedStatusCode": 200 | 201 | 204,
  "matchMode": "Subset" | "StatusOnly",
  "score": <number with at most 1 decimal>
}

⚠️ CRITICAL — THE MOST COMMON MISTAKE — READ CAREFULLY:

inputJson and expectJson MUST be a JSON STRING (double-quoted), NOT a JSON object or array.
Every " inside the value MUST be escaped as \".

  ✅ CORRECT: "expectJson": "{\"success\":true}"
  ✅ CORRECT: "inputJson": "{\"studentId\":1,\"courseId\":1}"
  ❌ WRONG:   "expectJson": {"success":true}            ← bare object → 400 Bad Request
  ❌ WRONG:   "inputJson": {"studentId":1}              ← bare object → 400 Bad Request

  No value → write: "inputJson": null   (bare null, no quotes around it)

MANDATORY SELF-CHECK before outputting:
  For every object in the array, verify:
  1. "inputJson" value starts with " or is null  (never starts with {)
  2. "expectJson" value starts with " or is null (never starts with {)
  3. "expectedStatusCode" is present (200, 201, or 204) — never omit this field
  4. No trailing comma after the last field in an object
  5. No trailing comma after the last object in the array

=== SCORING ===

Total score must equal EXACTLY 10.0. Verify sum before outputting.
  GROUP A (SOURCE): 2.0 pts
  GROUP B (HTTP):   8.0 pts

=== GROUP A — SOURCE CHECKS (3–5 cases, 2.0 pts total) ===

Read the lab spec and generate SOURCE test cases that verify the architecture and structure requirements described.
Always include these 2 mandatory cases:

  { urlTemplate:"project-count:3",                  description:"Solution has 3 .csproj files (3-layer architecture)" }
  { urlTemplate:"file-exists:**/docker-compose.yml", description:"docker-compose.yml present in submission"            }

Then add 1–3 more SOURCE cases based on what the spec explicitly requires. Use these rules:

  | Rule                  | urlTemplate pattern                              | When to use                                    |
  | project-name          | project-name:PRN232.*.API                        | spec mandates a specific naming convention      |
  | folder-exists         | folder-exists:**/Controllers                     | spec requires a specific folder structure       |
  | file-exists           | file-exists:**/Dockerfile                        | spec requires a specific file to be present     |
  | file-contains         | file-contains:**/Services/*.cs:interface         | spec requires use of interfaces/DI              |
  | file-not-contains     | file-not-contains:**/Controllers/*.cs:DbContext  | spec forbids business logic or DB in a layer    |

All SOURCE cases use: httpMethod:"SOURCE", expectedStatusCode:200, matchMode:"StatusOnly", expectJson:null, inputJson:null
Distribute 2.0 pts across all SOURCE cases (e.g. 0.5 + 1.0 + 0.5).

=== GROUP B — HTTP CASES (8.0 pts, max 2 cases per entity per operation) ===

KEEP IT SIMPLE. For each entity in the spec, generate at most 2 HTTP test cases total.
Choose the most representative operations: prefer GET list and POST. Add DELETE only if score budget allows.

Rules:
- GET list: use query params matching the spec (search, page, sort, expand if required). expectJson: "{\"success\":true}", matchMode: Subset
- POST: use a realistic request body. expectedStatusCode: 201 if spec says Created, else 200. expectJson: "{\"success\":true}", matchMode: Subset
- DELETE: use a HIGH seed ID (see below). expectedStatusCode: 204, matchMode: StatusOnly, expectJson: null
- GET by ID: use seed id=1. expectJson: "{\"success\":true}", matchMode: Subset
- Do NOT test 400/404 error cases
- Do NOT use {id} placeholder — always use a concrete number

SEED IDs:
  Semesters:   use id=1 for GET/PUT, id=5 for DELETE
  Students:    use id=1 for GET/PUT, id=50 for DELETE
  Subjects:    use id=1 for GET/PUT, id=10 for DELETE
  Courses:     use id=1 for GET/PUT, id=20 for DELETE
  Enrollments: use id=1 for GET/PUT, id=500 for DELETE
  Other:       use id=1 for GET/PUT, id=99 for DELETE

SCORE DISTRIBUTION for GROUP B (must sum to 8.0):
  Assign each case a score of 0.5, 1.0, 1.5, or 2.0.
  More important or complex endpoints should score higher.
  Make sure the total of all GROUP B cases equals exactly 8.0.

=== DO NOT ===
- Do NOT generate more than 2 HTTP cases per entity
- Do NOT add negative/error test cases (no 400, 404)
- Do NOT use {id} in urlTemplate
- Do NOT wrap output in markdown fences
- Do NOT output anything besides the JSON array

=== LAB SPEC ===

[PASTE NỘI DUNG FILE ĐỀ BÀI .MD VÀO ĐÂY]
````

---

## Ví dụ output mẫu (Lab 1 — LMS)

Đây là mảng JSON mẫu AI nên trả về — **13 case, tổng = 10.0 điểm**:

```json
[
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "project-count:3",
    "description": "Solution has 3 .csproj files (3-layer)",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 0.5
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-exists:**/docker-compose.yml",
    "description": "docker-compose.yml present",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 1.0
  },
  {
    "httpMethod": "SOURCE",
    "urlTemplate": "file-not-contains:**/Controllers/*.cs:DbContext",
    "description": "Controllers do not access DbContext directly",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 200,
    "matchMode": "StatusOnly",
    "score": 0.5
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/semesters",
    "description": "List semesters with paging",
    "inputJson": "{\"page\":1,\"size\":5}",
    "expectJson": "{\"success\":true,\"data\":{\"pagination\":{\"page\":1}}}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/semesters",
    "description": "Create a semester",
    "inputJson": "{\"semesterName\":\"Test Semester\",\"startDate\":\"2025-01-01T00:00:00\",\"endDate\":\"2025-06-30T00:00:00\"}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/students",
    "description": "List students with search",
    "inputJson": "{\"search\":\"nguyen\",\"page\":1,\"size\":5}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/students",
    "description": "Create a student",
    "inputJson": "{\"fullName\":\"Nguyen Van A\",\"email\":\"a@test.com\",\"dateOfBirth\":\"2000-01-01T00:00:00\"}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/subjects",
    "description": "List subjects with paging",
    "inputJson": "{\"page\":1,\"size\":5}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 0.5
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/subjects",
    "description": "Create a subject",
    "inputJson": "{\"subjectCode\":\"SE001\",\"subjectName\":\"Software Engineering\",\"credit\":3}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 0.5
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/courses",
    "description": "List courses with paging",
    "inputJson": "{\"page\":1,\"size\":5}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 0.5
  },
  {
    "httpMethod": "POST",
    "urlTemplate": "/api/courses",
    "description": "Create a course",
    "inputJson": "{\"courseName\":\"Test Course\",\"semesterId\":1}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 201,
    "matchMode": "Subset",
    "score": 0.5
  },
  {
    "httpMethod": "GET",
    "urlTemplate": "/api/enrollments",
    "description": "List enrollments with expand",
    "inputJson": "{\"expand\":\"student,course\",\"page\":1,\"size\":10}",
    "expectJson": "{\"success\":true}",
    "expectedStatusCode": 200,
    "matchMode": "Subset",
    "score": 1.0
  },
  {
    "httpMethod": "DELETE",
    "urlTemplate": "/api/enrollments/500",
    "description": "Delete an enrollment",
    "inputJson": null,
    "expectJson": null,
    "expectedStatusCode": 204,
    "matchMode": "StatusOnly",
    "score": 1.0
  }
]
```

---

## Import vào hệ thống

Sau khi có mảng JSON, paste vào Swagger UI hoặc dùng curl:

```bash
curl -X POST http://localhost:5049/api/lab-assignments/{assignment-id}/testcases/batch \
  -H "Content-Type: application/json" \
  -d '[...mảng JSON...]'
```

**Sau khi import:**

- Tất cả test case ở trạng thái `Draft` — cần approve trước khi chấm
- `PATCH /api/lab-assignments/{id}/testcases/approve-all` để approve toàn bộ Draft → Approved một lần
- Hoặc `PATCH /api/lab-testcases/{id}/status` với body `{"status":"Approved"}` để approve từng cái
- Kiểm tra tổng `score` khớp với rubric của lab trước khi bấm Grade
