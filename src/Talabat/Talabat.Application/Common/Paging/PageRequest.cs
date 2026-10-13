namespace Talabat.Application.Common.Paging;

/// <summary>
/// A normalized page request. Out-of-range values are clamped rather than rejected so that
/// list endpoints stay tolerant of client input (page &lt; 1 becomes 1; page size is bounded
/// to 1..<see cref="MaxPageSize"/>), as documented in the Customer API contract.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 50;

    // Keeps (Page - 1) * PageSize within int range.
    private const int MaxPage = int.MaxValue / MaxPageSize;

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        Page = Math.Clamp(page, 1, MaxPage);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Page { get; }

    public int PageSize { get; }

    public int Skip => (Page - 1) * PageSize;
}
