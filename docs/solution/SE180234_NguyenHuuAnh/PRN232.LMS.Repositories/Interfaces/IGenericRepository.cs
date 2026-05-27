namespace PRN232.LMS.Repositories.Interfaces;

public interface IGenericRepository<T> where T : class
{
    IQueryable<T> GetQueryable();
    Task<T?> GetByIdAsync(int id);
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    Task<bool> ExistsAsync(int id);
}
