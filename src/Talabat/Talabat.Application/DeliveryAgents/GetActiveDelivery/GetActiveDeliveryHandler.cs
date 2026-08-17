using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetActiveDelivery;

public sealed class GetActiveDeliveryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUserRepository _userRepository;

    public GetActiveDeliveryHandler(
        IDeliveryRepository deliveryRepository,
        IUserRepository userRepository)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<UseCaseResult<ActiveDeliveryDto>> Handle(
        GetActiveDeliveryQuery query,
        CancellationToken cancellationToken = default)
    {
        var agentId = query.AgentId;

        var delivery = await _deliveryRepository.GetActiveByAgentIdAsync(agentId, cancellationToken);

        if (delivery is null)
        {
            return UseCaseResult<ActiveDeliveryDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryNotFound,
                    "No active delivery found for this agent."));
        }

        var customer = await _userRepository.GetByIdReadOnlyAsync(
            delivery.CustomerId,
            cancellationToken);

        if (customer is null)
        {
            return UseCaseResult<ActiveDeliveryDto>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.UserNotFound,
                    "Delivery customer was not found."));
        }

        var dto = new ActiveDeliveryDto(
            delivery.Id,
            delivery.OrderId,
            delivery.RestaurantId,
            delivery.Status,
            delivery.RestaurantName,
            delivery.RestaurantPickupAddress.Street,
            delivery.RestaurantPickupAddress.City,
            delivery.RestaurantPickupAddress.BuildingNumber,
            delivery.RestaurantPickupAddress.Floor,
            delivery.DeliveryAddress.Street,
            delivery.DeliveryAddress.City,
            delivery.DeliveryAddress.BuildingNumber,
            delivery.DeliveryAddress.Floor,
            customer.PhoneNumber,
            delivery.AssignedAt);

        return UseCaseResult<ActiveDeliveryDto>.Success(dto);
    }
}
