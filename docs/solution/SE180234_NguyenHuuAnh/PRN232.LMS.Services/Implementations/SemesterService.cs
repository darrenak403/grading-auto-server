using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Helpers;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Implementations;

public class SemesterService : ISemesterService
{
    private readonly ISemesterRepository _repo;

    public SemesterService(ISemesterRepository repo) => _repo = repo;

    public async Task<PagedResult<SemesterBusinessModel>> GetAllAsync(QueryParameters query)
    {
        var q = _repo.GetQueryable();

        // Expand
        var expandList = query.Expand?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => e.Trim().ToLower()).ToList() ?? [];

        if (expandList.Contains("courses"))
            q = q.Include(s => s.Courses);

        // Search
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(s => s.SemesterName.Contains(query.Search));

        // Sort
        q = QueryHelper.ApplySorting(q, query.Sort);

        var paged = await QueryHelper.ApplyPagingAsync(q, query);

        return new PagedResult<SemesterBusinessModel>
        {
            Items = paged.Items.Select(s => MapToBusinessModel(s, expandList)),
            Pagination = paged.Pagination
        };
    }

    public async Task<SemesterBusinessModel?> GetByIdAsync(int id)
    {
        var semester = await _repo.GetQueryable()
            .Include(s => s.Courses)
            .FirstOrDefaultAsync(s => s.SemesterId == id);

        return semester == null ? null : MapToBusinessModel(semester, ["courses"]);
    }

    public async Task<SemesterBusinessModel> CreateAsync(SemesterBusinessModel model)
    {
        var entity = new Semester
        {
            SemesterName = model.SemesterName,
            StartDate    = model.StartDate,
            EndDate      = model.EndDate
        };
        var created = await _repo.AddAsync(entity);
        return MapToBusinessModel(created, []);
    }

    public async Task<SemesterBusinessModel?> UpdateAsync(int id, SemesterBusinessModel model)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.SemesterName = model.SemesterName;
        entity.StartDate    = model.StartDate;
        entity.EndDate      = model.EndDate;

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

    private static SemesterBusinessModel MapToBusinessModel(Semester s, List<string> expand)
    {
        var model = new SemesterBusinessModel
        {
            SemesterId   = s.SemesterId,
            SemesterName = s.SemesterName,
            StartDate    = s.StartDate,
            EndDate      = s.EndDate,
            CourseCount  = s.Courses?.Count ?? 0
        };

        if (expand.Contains("courses") && s.Courses != null)
        {
            model.Courses = s.Courses.Select(c => new CourseBusinessModel
            {
                CourseId   = c.CourseId,
                CourseName = c.CourseName,
                SemesterId = c.SemesterId
            }).ToList();
        }

        return model;
    }
}
