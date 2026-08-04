using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Helpers;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Implementations;

public class StudentService : IStudentService
{
    private readonly IStudentRepository _repo;

    public StudentService(IStudentRepository repo) => _repo = repo;

    public async Task<PagedResult<StudentBusinessModel>> GetAllAsync(QueryParameters query)
    {
        var q = _repo.GetQueryable();

        var expandList = query.Expand?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower()).ToList() ?? [];

        if (expandList.Contains("enrollments"))
            q = q.Include(s => s.Enrollments).ThenInclude(e => e.Course).ThenInclude(c => c.Semester);

        // Search
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(s => s.FullName.Contains(query.Search)
                          || s.Email.Contains(query.Search));

        // Sort
        q = QueryHelper.ApplySorting(q, query.Sort);

        var paged = await QueryHelper.ApplyPagingAsync(q, query);

        return new PagedResult<StudentBusinessModel>
        {
            Items      = paged.Items.Select(s => MapToBusinessModel(s, expandList)),
            Pagination = paged.Pagination
        };
    }

    public async Task<StudentBusinessModel?> GetByIdAsync(int id)
    {
        var student = await _repo.GetQueryable()
            .Include(s => s.Enrollments).ThenInclude(e => e.Course).ThenInclude(c => c.Semester)
            .FirstOrDefaultAsync(s => s.StudentId == id);

        return student == null ? null : MapToBusinessModel(student, ["enrollments"]);
    }

    public async Task<StudentBusinessModel> CreateAsync(StudentBusinessModel model)
    {
        var entity = new Student
        {
            FullName    = model.FullName,
            Email       = model.Email,
            DateOfBirth = model.DateOfBirth
        };
        var created = await _repo.AddAsync(entity);
        return MapToBusinessModel(created, []);
    }

    public async Task<StudentBusinessModel?> UpdateAsync(int id, StudentBusinessModel model)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.FullName    = model.FullName;
        entity.Email       = model.Email;
        entity.DateOfBirth = model.DateOfBirth;

        var updated = await _repo.UpdateAsync(entity);
        return MapToBusinessModel(updated, []);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;
        await _repo.DeleteAsync(entity);
        return true;
    }

    private static StudentBusinessModel MapToBusinessModel(Student s, List<string> expand)
    {
        var model = new StudentBusinessModel
        {
            StudentId       = s.StudentId,
            FullName        = s.FullName,
            Email           = s.Email,
            DateOfBirth     = s.DateOfBirth,
            EnrollmentCount = s.Enrollments?.Count ?? 0
        };

        if (expand.Contains("enrollments") && s.Enrollments != null)
        {
            model.Enrollments = s.Enrollments.Select(e => new EnrollmentBusinessModel
            {
                EnrollmentId = e.EnrollmentId,
                StudentId    = e.StudentId,
                CourseId     = e.CourseId,
                EnrollDate   = e.EnrollDate,
                Status       = e.Status,
                Course = e.Course == null ? null : new CourseBusinessModel
                {
                    CourseId   = e.Course.CourseId,
                    CourseName = e.Course.CourseName,
                    SemesterId = e.Course.SemesterId,
                    Semester = e.Course.Semester == null ? null : new SemesterBusinessModel
                    {
                        SemesterId   = e.Course.Semester.SemesterId,
                        SemesterName = e.Course.Semester.SemesterName,
                        StartDate    = e.Course.Semester.StartDate,
                        EndDate      = e.Course.Semester.EndDate
                    }
                }
            }).ToList();
        }

        return model;
    }
}
