using Talabat.Application.Common.Paging;
using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Mapping;
using Talabat.Application.Ordering.Models;
using Talabat.Application.Ordering.Status;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.GetOrderHistory;

public sealed class GetOrderHistoryHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IDeliveryRepository _deliveryRepository;

    public GetOrderHistoryHandler(
        IOrderRepository orderRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IDeliveryRepository deliveryRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
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

        var orderIds = orders.Select(order => order.Id).ToList();
        var fulfillments = (await _fulfillmentRepository.GetByOrderIdsAsync(orderIds, cancellationToken))
            .ToDictionary(fulfillment => fulfillment.OrderId);
        var deliveries = (await _deliveryRepository.GetByOrderIdsAsync(orderIds, cancellationToken))
            .ToDictionary(delivery => delivery.OrderId);

        var summaries = orders
            .Select(order =>
            {
                if (!fulfillments.TryGetValue(order.Id, out var fulfillment))
                {
                    throw new InvalidOperationException($"Order {order.Id} has no order fulfillment.");
                }

                var progress = CustomerOrderStatusMapper.Map(fulfillment, deliveries.GetValueOrDefault(order.Id));
                return OrderMapper.ToSummary(order, progress);
            })
            .ToList()
            .AsReadOnly();

        return UseCaseResult<PagedResult<OrderSummary>>.Success(
            new PagedResult<OrderSummary>(summaries, page.Page, page.PageSize, totalCount));
    }
}
