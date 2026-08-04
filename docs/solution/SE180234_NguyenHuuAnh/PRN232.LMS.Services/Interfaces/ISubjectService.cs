using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Interfaces;

public interface ISubjectService
{
    Task<PagedResult<SubjectBusinessModel>> GetAllAsync(QueryParameters query);
    Task<SubjectBusinessModel?> GetByIdAsync(int id);
    Task<SubjectBusinessModel> CreateAsync(SubjectBusinessModel model);
    Task<SubjectBusinessModel?> UpdateAsync(int id, SubjectBusinessModel model);
    Task<bool> DeleteAsync(int id);
}
