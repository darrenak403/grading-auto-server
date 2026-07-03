using System.Linq.Expressions;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;

namespace GradingSystem.Tests.Fakes;

/// <summary>
/// Minimal in-memory stand-in for IGenericRepository&lt;T&gt;, backed by a plain List&lt;T&gt;.
/// Good enough to exercise Application-layer service logic without a real database.
/// </summary>
public class FakeRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    public readonly List<T> Items = [];

    public Task<T?> GetByIdAsync(Guid id) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public Task<IEnumerable<T>> GetAllAsync() =>
        Task.FromResult<IEnumerable<T>>(Items.ToList());

    public Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate) =>
        // Eagerly materialize, matching the real EF Core repository's ToListAsync() contract:
        // callers may mutate Items (Add/Remove) while iterating the result.
        Task.FromResult<IEnumerable<T>>(Items.AsQueryable().Where(predicate).ToList());

    public Task AddAsync(T entity)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        var idx = Items.FindIndex(i => i.Id == entity.Id);
        if (idx >= 0) Items[idx] = entity;
    }

    public void Remove(T entity) => Items.RemoveAll(i => i.Id == entity.Id);
}
