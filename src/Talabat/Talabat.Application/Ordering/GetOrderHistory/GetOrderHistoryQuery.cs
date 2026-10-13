using Talabat.Application.Common.Paging;

namespace Talabat.Application.Ordering.GetOrderHistory;

public sealed record GetOrderHistoryQuery(
    int CustomerId,
    int Page = 1,
    int PageSize = PageRequest.DefaultPageSize);
