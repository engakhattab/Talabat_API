using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.RestaurantOperations;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ProgressPickup;

public sealed class PickUpOrderHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public PickUpOrderHandler(
        IDeliveryRepository deliveryRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<int>> Handle(
        PickUpOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        var delivery = await _deliveryRepository.GetByIdForAgentAsync(command.DeliveryId, command.AgentId, cancellationToken);

        if (delivery is null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryNotFound,
                    "Delivery was not found."));
        }

        // A delivery exists only after acceptance, so its fulfillment always exists (design D3).
        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(delivery.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Delivery {delivery.Id} has no fulfillment for order {delivery.OrderId}.");

        // Pickup gate (D5, BR-DEL-019): the food leaves the restaurant only once it is ready.
        if (fulfillment.Status != OrderFulfillmentStatus.ReadyForPickup)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.Conflict(
                    ApplicationErrorCodes.FulfillmentNotReady,
                    "The order is not ready for pickup yet."));
        }

        var now = _clock.UtcNow;

        try
        {
            delivery.MarkPickedUp(command.AgentId, now);
            fulfillment.HandOver(delivery.RestaurantId, now);
        }
        catch (DomainException ex)
        {
            return UseCaseResult<int>.Failure(DomainExceptionMapper.Map(ex));
        }

        // Pickup and hand-over commit together or not at all (D5, D12).
        await _unitOfWork.ExecuteInTransactionAsync(
            ct => _unitOfWork.SaveChangesAsync(ct),
            cancellationToken);

        return UseCaseResult<int>.Success(delivery.Id);
    }
}
