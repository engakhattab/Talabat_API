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

    public async Task<UseCaseResult<IReadOnlyCollection<OrderSummary>>> Handle(
        GetOrderHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var orders = await _orderRepository.GetByCustomerIdAsync(
            query.CustomerId,
            cancellationToken);

        var summaries = orders
            .OrderByDescending(order => order.CreatedAt)
            .Select(OrderMapper.ToSummary)
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<OrderSummary>>.Success(summaries);
    }
}
