using Microsoft.EntityFrameworkCore;

namespace Energoreestr.Api;

public record Page<T>(int Total, int PageNumber, int PageSize, List<T> Items);

public static class Paging
{
    public const int DefaultSize = 20;
    public const int MaxSize = 100;

    /// <summary>Рахує загальну кількість і віддає одну сторінку. Запит має бути вже відсортований.</summary>
    public static async Task<Page<T>> ToPageAsync<T>(this IQueryable<T> query, int? page, int? pageSize, CancellationToken ct)
    {
        int number = Math.Max(page ?? 1, 1);
        int size = Math.Clamp(pageSize ?? DefaultSize, 1, MaxSize);

        int total = await query.CountAsync(ct);
        var items = await query.Skip((number - 1) * size).Take(size).ToListAsync(ct);

        return new Page<T>(total, number, size, items);
    }
}
