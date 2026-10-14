using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Mapping;
using Talabat.Application.Ordering.Models;
using Talabat.Application.Ordering.Status;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.GetOrderDetails;

public sealed class GetOrderDetailsHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IDeliveryRepository _deliveryRepository;

    public GetOrderDetailsHandler(
        IOrderRepository orderRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IDeliveryRepository deliveryRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
    }

    public async Task<UseCaseResult<OrderDetails>> Handle(
        GetOrderDetailsQuery query,
        CancellationToken cancellationToken = default)
    {
        var order = await _orderRepository.GetByIdForCustomerAsync(
            query.OrderId,
            query.CustomerId,
            cancellationToken);

        if (order is null)
        {
            return UseCaseResult<OrderDetails>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.OrderNotFound,
                    "Order was not found."));
        }

        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(order.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Order {order.Id} has no order fulfillment.");
        var delivery = await _deliveryRepository.GetByOrderIdAsync(order.Id, cancellationToken);
        var progress = CustomerOrderStatusMapper.Map(fulfillment, delivery);

        return UseCaseResult<OrderDetails>.Success(OrderMapper.ToDetails(order, progress));
    }
}
