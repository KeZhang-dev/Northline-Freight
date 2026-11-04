using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Northline.Api.Dtos;
using Northline.Api.Entities;

namespace Northline.Api.Services;

internal static class QueryHelpers
{
    /// <summary>ILIKE pattern with user wildcards escaped.</summary>
    public static string? LikePattern(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }
        var escaped = search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        return $"%{escaped}%";
    }

    /// <summary>Urgent=3, High=2, Normal=1, Low=0, so priority sorts by business importance, not alphabetically.</summary>
    public static readonly Expression<Func<TransportOrder, int>> PriorityRank = o =>
        o.Priority == OrderPriority.Urgent ? 3 :
        o.Priority == OrderPriority.High ? 2 :
        o.Priority == OrderPriority.Normal ? 1 : 0;

    public static IOrderedQueryable<T> OrderByDir<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, SortDirection dir) =>
        dir == SortDirection.Desc ? query.OrderByDescending(key) : query.OrderBy(key);

    public static async Task<PagedResult<TOut>> ToPagedAsync<TIn, TOut>(
        this IQueryable<TIn> query, PageQuery page, Func<List<TIn>, Task<IReadOnlyList<TOut>>> map, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<TOut>(await map(items), page.Page, page.PageSize, total);
    }

    public static IQueryable<DispatchAssignment> WithDetails(this IQueryable<DispatchAssignment> query) =>
        query.AsNoTracking()
            .Include(a => a.Order)
            .Include(a => a.Driver)
            .Include(a => a.Vehicle)
            .Include(a => a.CreatedBy)
            .Include(a => a.ClosedBy)
            .Include(a => a.PreviousAssignment);
}
