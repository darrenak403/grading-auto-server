using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.LMS.Services.Models;

namespace PRN232.LMS.Services.Helpers;

/// <summary>
/// Reusable helper for applying search/sort/paging to IQueryable sources
/// </summary>
public static class QueryHelper
{
    /// <summary>Apply paging and return PagedResult</summary>
    public static async Task<PagedResult<T>> ApplyPagingAsync<T>(
        IQueryable<T> query, QueryParameters parameters)
    {
        int total = await query.CountAsync();
        int page  = parameters.Page < 1 ? 1 : parameters.Page;
        int size  = parameters.Size;

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            Pagination = new PaginationMetadata
            {
                Page       = page,
                PageSize   = size,
                TotalItems = total,
                TotalPages = (int)Math.Ceiling(total / (double)size)
            }
        };
    }

    /// <summary>Apply dynamic sorting based on sort string: "field1,-field2"</summary>
    public static IQueryable<T> ApplySorting<T>(IQueryable<T> query, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return query;

        var fields = sort.Split(',', StringSplitOptions.RemoveEmptyEntries);
        IOrderedQueryable<T>? ordered = null;

        foreach (var field in fields)
        {
            bool descending = field.StartsWith('-');
            string fieldName = descending ? field[1..] : field;

            var param = Expression.Parameter(typeof(T), "x");
            Expression? prop = null;

            // Case-insensitive property lookup
            var propInfo = typeof(T).GetProperties()
                .FirstOrDefault(p => p.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

            if (propInfo == null) continue;

            prop = Expression.Property(param, propInfo.Name);
            var lambda = Expression.Lambda(prop, param);

            if (ordered == null)
            {
                ordered = descending
                    ? Queryable.OrderByDescending(query, (dynamic)lambda)
                    : Queryable.OrderBy(query, (dynamic)lambda);
            }
            else
            {
                ordered = descending
                    ? Queryable.ThenByDescending(ordered, (dynamic)lambda)
                    : Queryable.ThenBy(ordered, (dynamic)lambda);
            }
        }

        return ordered ?? query;
    }
}
