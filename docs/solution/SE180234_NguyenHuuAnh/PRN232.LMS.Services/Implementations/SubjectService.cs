using PRN232.LMS.Repositories.Entities;
using PRN232.LMS.Repositories.Interfaces;
using PRN232.LMS.Services.Helpers;
using PRN232.LMS.Services.Interfaces;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Implementations;

public class SubjectService : ISubjectService
{
    private readonly ISubjectRepository _repo;

    public SubjectService(ISubjectRepository repo) => _repo = repo;

    public async Task<PagedResult<SubjectBusinessModel>> GetAllAsync(QueryParameters query)
    {
        var q = _repo.GetQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(s => s.SubjectName.Contains(query.Search)
                          || s.SubjectCode.Contains(query.Search));

        // Sort
        q = QueryHelper.ApplySorting(q, query.Sort);

        var paged = await QueryHelper.ApplyPagingAsync(q, query);

        return new PagedResult<SubjectBusinessModel>
        {
            Items      = paged.Items.Select(MapToBusinessModel),
            Pagination = paged.Pagination
        };
    }

    public async Task<SubjectBusinessModel?> GetByIdAsync(int id)
    {
        var subject = await _repo.GetByIdAsync(id);
        return subject == null ? null : MapToBusinessModel(subject);
    }

    public async Task<SubjectBusinessModel> CreateAsync(SubjectBusinessModel model)
    {
        var entity = new Subject
        {
            SubjectCode = model.SubjectCode,
            SubjectName = model.SubjectName,
            Credit      = model.Credit
        };
        var created = await _repo.AddAsync(entity);
        return MapToBusinessModel(created);
    }

    public async Task<SubjectBusinessModel?> UpdateAsync(int id, SubjectBusinessModel model)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return null;

        entity.SubjectCode = model.SubjectCode;
        entity.SubjectName = model.SubjectName;
        entity.Credit      = model.Credit;

        var updated = await _repo.UpdateAsync(entity);
        return MapToBusinessModel(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var entity = await _repo.GetByIdAsync(id);
        if (entity == null) return false;
        await _repo.DeleteAsync(entity);
        return true;
    }

    private static SubjectBusinessModel MapToBusinessModel(Subject s) => new()
    {
        SubjectId   = s.SubjectId,
        SubjectCode = s.SubjectCode,
        SubjectName = s.SubjectName,
        Credit      = s.Credit
    };
}
