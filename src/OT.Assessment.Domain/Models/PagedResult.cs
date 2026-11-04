namespace OT.Assessment.Domain.Models;

public class PagedResult<T>
{
    public required IReadOnlyList<T> Data { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int Total { get; init; }

    public required int TotalPages { get; init; }

    public static PagedResult<T> Create(IReadOnlyList<T> data, int page, int pageSize, int total)
    {
        int totalPages = total / pageSize;

        bool hasPartialLastPage = total % pageSize > 0;
        if (hasPartialLastPage)
        {
            totalPages = totalPages + 1;
        }

        return new PagedResult<T>
        {
            Data = data,
            Page = page,
            PageSize = pageSize,
            Total = total,
            TotalPages = totalPages
        };
    }
}
