using Talabat.Application.Common.Paging;
using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Mapping;
using Talabat.Application.Ordering.Models;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.GetOrderHistory;

public sealed class GetOrderHistoryHandler
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderHistoryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
    }

    public async Task<UseCaseResult<PagedResult<OrderSummary>>> Handle(
        GetOrderHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var (orders, totalCount) = await _orderRepository.GetPageByCustomerIdAsync(
            query.CustomerId,
            page.Skip,
            page.PageSize,
            cancellationToken);

        var summaries = orders
            .Select(OrderMapper.ToSummary)
            .ToList()
            .AsReadOnly();

        return UseCaseResult<PagedResult<OrderSummary>>.Success(
            new PagedResult<OrderSummary>(summaries, page.Page, page.PageSize, totalCount));
    }
}
