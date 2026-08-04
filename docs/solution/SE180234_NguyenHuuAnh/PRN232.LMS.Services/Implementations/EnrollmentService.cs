using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Helpers;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Implementations;

public class EnrollmentService : IEnrollmentService
{
    private readonly IEnrollmentRepository _repo;

    public EnrollmentService(IEnrollmentRepository repo) => _repo = repo;

    public async Task<PagedResult<EnrollmentBusinessModel>> GetAllAsync(QueryParameters query)
    {
        var q = _repo.GetQueryable();

        var expandList = query.Expand?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower()).ToList() ?? [];

        if (expandList.Contains("student"))
            q = q.Include(e => e.Student);
        if (expandList.Contains("course"))
            q = q.Include(e => e.Course).ThenInclude(c => c.Semester);

        // Search by status
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(e => e.Status.Contains(query.Search)
                          || e.Student.FullName.Contains(query.Search)
                          || e.Course.CourseName.Contains(query.Search));

        // Sort
        q = QueryHelper.ApplySorting(q, query.Sort);

        var paged = await QueryHelper.ApplyPagingAsync(q, query);

        return new PagedResult<EnrollmentBusinessModel>
        {
            Items      = paged.Items.Select(e => MapToBusinessModel(e, expandList)),
            Pagination = paged.Pagination
        };
    }

    public async Task<EnrollmentBusinessModel?> GetByIdAsync(int id)
    {
        var enrollment = await _repo.GetQueryable()
            .Include(e => e.Student)
            .Include(e => e.Course).ThenInclude(c => c.Semester)
            .FirstOrDefaultAsync(e => e.EnrollmentId == id);

        return enrollment == null ? null : MapToBusinessModel(enrollment, ["student", "course"]);
    }

    public async Task<EnrollmentBusinessModel> CreateAsync(EnrollmentBusinessModel model)
    {
        var entity = new Enrollment
        {
            StudentId  = model.StudentId,
            CourseId   = model.CourseId,
            EnrollDate = model.EnrollDate,
            Status     = model.Status
        };
        var created = await _repo.AddAsync(entity);
        return MapToBusinessModel(created, []);
    }

    public async Task<EnrollmentBusinessModel?> UpdateAsync(int id, EnrollmentBusinessModel model)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.StudentId  = model.StudentId;
        entity.CourseId   = model.CourseId;
        entity.EnrollDate = model.EnrollDate;
        entity.Status     = model.Status;

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

    private static EnrollmentBusinessModel MapToBusinessModel(Enrollment e, List<string> expand)
    {
        var model = new EnrollmentBusinessModel
        {
            EnrollmentId = e.EnrollmentId,
            StudentId    = e.StudentId,
            CourseId     = e.CourseId,
            EnrollDate   = e.EnrollDate,
            Status       = e.Status
        };

        if (expand.Contains("student") && e.Student != null)
        {
            model.Student = new StudentBusinessModel
            {
                StudentId   = e.Student.StudentId,
                FullName    = e.Student.FullName,
                Email       = e.Student.Email,
                DateOfBirth = e.Student.DateOfBirth
            };
        }

        if (expand.Contains("course") && e.Course != null)
        {
            model.Course = new CourseBusinessModel
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
            };
        }

        return model;
    }
}
