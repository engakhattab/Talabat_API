using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Application.Deliveries;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Fulfillment.AcceptOrder;

public sealed class AcceptOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AcceptOrderHandler(
        IOrderRepository orderRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IDeliveryRepository deliveryRepository,
        IRestaurantRepository restaurantRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<AcceptedOrder>> Handle(
        AcceptOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        var order = fulfillment is null
            ? null
            : await _orderRepository.GetByIdAsync(command.OrderId, cancellationToken);

        if (fulfillment is null || order is null)
        {
            return UseCaseResult<AcceptedOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.OrderNotFound, "Order was not found."));
        }

        var restaurant = await _restaurantRepository.GetByIdAsync(fulfillment.RestaurantId, cancellationToken);
        if (restaurant is null)
        {
            return UseCaseResult<AcceptedOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.RestaurantNotFound, "Restaurant was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            // An Admin acts on behalf of the order's own restaurant (D11); a restaurant may accept only its orders.
            fulfillment.Accept(command.Actor.ActingRestaurantId(fulfillment), now);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<AcceptedOrder>.Failure(DomainExceptionMapper.Map(exception));
        }

        // Acceptance creates the order's delivery (design D3): the pickup snapshot is taken from the restaurant
        // now, the drop-off address from the order. Both commit together or not at all (D12).
        var delivery = DeliveryFactory.ForOrder(
            order.Id,
            order.CustomerId,
            restaurant,
            order.DeliveryAddress,
            now);

        await _unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await _deliveryRepository.AddAsync(delivery, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);

        return UseCaseResult<AcceptedOrder>.Success(new AcceptedOrder(order.Id, delivery.Id, now));
    }
}
