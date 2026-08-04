using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Helpers;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Implementations;

public class CourseService : ICourseService
{
    private readonly ICourseRepository _repo;

    public CourseService(ICourseRepository repo) => _repo = repo;

    public async Task<PagedResult<CourseBusinessModel>> GetAllAsync(QueryParameters query)
    {
        var q = _repo.GetQueryable();

        var expandList = query.Expand?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower()).ToList() ?? [];

        if (expandList.Contains("semester"))
            q = q.Include(c => c.Semester);
        if (expandList.Contains("enrollments"))
            q = q.Include(c => c.Enrollments).ThenInclude(e => e.Student);

        // Search
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(c => c.CourseName.Contains(query.Search));

        // Sort
        q = QueryHelper.ApplySorting(q, query.Sort);

        var paged = await QueryHelper.ApplyPagingAsync(q, query);

        return new PagedResult<CourseBusinessModel>
        {
            Items = paged.Items.Select(c => MapToBusinessModel(c, expandList)),
            Pagination = paged.Pagination
        };
    }

    public async Task<CourseBusinessModel?> GetByIdAsync(int id)
    {
        var course = await _repo.GetQueryable()
            .Include(c => c.Semester)
            .Include(c => c.Enrollments).ThenInclude(e => e.Student)
            .FirstOrDefaultAsync(c => c.CourseId == id);

        return course == null ? null : MapToBusinessModel(course, ["semester", "enrollments"]);
    }

    public async Task<CourseBusinessModel> CreateAsync(CourseBusinessModel model)
    {
        var entity = new Course
        {
            CourseName = model.CourseName,
            SemesterId = model.SemesterId
        };
        var created = await _repo.AddAsync(entity);
        return MapToBusinessModel(created, []);
    }

    public async Task<CourseBusinessModel?> UpdateAsync(int id, CourseBusinessModel model)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.CourseName = model.CourseName;
        entity.SemesterId = model.SemesterId;

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

    private static CourseBusinessModel MapToBusinessModel(Course c, List<string> expand)
    {
        var model = new CourseBusinessModel
        {
            CourseId        = c.CourseId,
            CourseName      = c.CourseName,
            SemesterId      = c.SemesterId,
            EnrollmentCount = c.Enrollments?.Count ?? 0
        };

        if (expand.Contains("semester") && c.Semester != null)
        {
            model.Semester = new SemesterBusinessModel
            {
                SemesterId   = c.Semester.SemesterId,
                SemesterName = c.Semester.SemesterName,
                StartDate    = c.Semester.StartDate,
                EndDate      = c.Semester.EndDate
            };
        }

        if (expand.Contains("enrollments") && c.Enrollments != null)
        {
            model.Enrollments = c.Enrollments.Select(e => new EnrollmentBusinessModel
            {
                EnrollmentId = e.EnrollmentId,
                StudentId    = e.StudentId,
                CourseId     = e.CourseId,
                EnrollDate   = e.EnrollDate,
                Status       = e.Status,
                Student = e.Student == null ? null : new StudentBusinessModel
                {
                    StudentId   = e.Student.StudentId,
                    FullName    = e.Student.FullName,
                    Email       = e.Student.Email,
                    DateOfBirth = e.Student.DateOfBirth
                }
            }).ToList();
        }

        return model;
    }
}
