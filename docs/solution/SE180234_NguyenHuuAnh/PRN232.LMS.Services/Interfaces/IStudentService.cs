using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Interfaces;

public interface IStudentService
{
    Task<PagedResult<StudentBusinessModel>> GetAllAsync(QueryParameters query);
    Task<StudentBusinessModel?> GetByIdAsync(int id);
    Task<StudentBusinessModel> CreateAsync(StudentBusinessModel model);
    Task<StudentBusinessModel?> UpdateAsync(int id, StudentBusinessModel model);
    Task<bool> DeleteAsync(int id);
}
