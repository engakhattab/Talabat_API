using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Fulfillment.AcceptOrder;

public sealed class AcceptOrderHandler
{
    public AcceptOrderHandler(
        IOrderRepository orderRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IDeliveryRepository deliveryRepository,
        IRestaurantRepository restaurantRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
    }

    public Task<UseCaseResult<AcceptedOrder>> Handle(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
