using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Interfaces;

public interface IEnrollmentService
{
    Task<PagedResult<EnrollmentBusinessModel>> GetAllAsync(QueryParameters query);
    Task<EnrollmentBusinessModel?> GetByIdAsync(int id);
    Task<EnrollmentBusinessModel> CreateAsync(EnrollmentBusinessModel model);
    Task<EnrollmentBusinessModel?> UpdateAsync(int id, EnrollmentBusinessModel model);
    Task<bool> DeleteAsync(int id);
}
