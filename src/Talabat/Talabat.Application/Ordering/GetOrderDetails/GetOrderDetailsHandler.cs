using Talabat.Application.Common.Results;
using Talabat.Application.Ordering.Mapping;
using Talabat.Application.Ordering.Models;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.GetOrderDetails;

public sealed class GetOrderDetailsHandler
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderDetailsHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
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

        return UseCaseResult<OrderDetails>.Success(OrderMapper.ToDetails(order));
    }
}
