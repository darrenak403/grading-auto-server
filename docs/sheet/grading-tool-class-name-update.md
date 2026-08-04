# Grading Tool Update: submissions.class_name

Tai lieu nay dung de cap nhat grading/local sync tool sau khi bang `submissions`
duoc bo sung cot `class_name`.

## Database migration

Chay script:

```sql
alter table submissions
add column if not exists class_name text;

update submissions s
set class_name = ae.class_name
from allowed_emails ae
where s.student_id = ae.student_id
  and s.class_name is null;

create index if not exists submissions_lab_class_idx
on submissions (lab_id, class_name);
```

File migration trong repo:

```text
docs/api/2026-07-01-add-class-name-to-submissions.sql
```

## Required payload change

Moi lan grading tool upsert vao bang `submissions`, payload phai gui them
`class_name`.

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

## Sync API update

API `POST /api/lab-assignments/{id}/sync-supabase` da ho tro nhan body de override
`lab_id` va `class_name` khi sync.

```json
{
  "labId": "Lab2",
  "className": "SE1815"
}
```

Neu khong truyen:

- `labId`: he thong tiep tuc dung `assignment.Title`
- `className`: he thong tiep tuc tra tu Supabase theo `student_id`

He thong khong con auto sync sau khi worker cham xong. Du lieu chi duoc day len
Supabase khi goi endpoint sync thu cong.

## Python example

```python
payload.append({
    "student_id": student_id,
    "lab_id": lab_id,
    "class_name": class_name,
    "score": item["Score"],
    "status": item["Status"],
    "details": item.get("TestDetails", {}),
})
```

## Missing first submission query

Dung roster trong `allowed_emails` de tim sinh vien thieu bai trong mot lop va
mot lab:

```sql
select ae.email, ae.student_id, ae.class_name
from allowed_emails ae
where ae.class_name = :class_name
  and not exists (
    select 1
    from submissions s
    where s.student_id = ae.student_id
      and s.lab_id = :lab_id
  );
```

## Request type rule

Khi student co trong `allowed_emails` nhung chua co dong trong `submissions`
cho `student_id + lab_id`, day la case nop lan dau bi tre.

Khi student da co dong trong `submissions` cho `student_id + lab_id`, day la
case resubmit/cham lai.
