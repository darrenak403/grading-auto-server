using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Interfaces;

public interface ICourseService
{
    Task<PagedResult<CourseBusinessModel>> GetAllAsync(QueryParameters query);
    Task<CourseBusinessModel?> GetByIdAsync(int id);
    Task<CourseBusinessModel> CreateAsync(CourseBusinessModel model);
    Task<CourseBusinessModel?> UpdateAsync(int id, CourseBusinessModel model);
    Task<bool> DeleteAsync(int id);
}
