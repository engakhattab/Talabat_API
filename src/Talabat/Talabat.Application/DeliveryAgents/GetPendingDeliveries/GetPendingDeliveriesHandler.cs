using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetPendingDeliveries;

public sealed class GetPendingDeliveriesHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICurrentUser _currentUser;

    public GetPendingDeliveriesHandler(
        IDeliveryRepository deliveryRepository,
        ICurrentUser currentUser)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>> Handle(
        GetPendingDeliveriesQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>.Failure(
                DomainExceptionMapper.OwnershipMismatch(
                    ApplicationErrorCodes.AgentRequired,
                    "Authentication required."));
        }

        var deliveries = await _deliveryRepository.GetPendingAssignmentAsync(cancellationToken);

        var dtos = deliveries
            .Select(d => new PendingDeliveryDto(
                d.Id,
                d.OrderId,
                d.CustomerId,
                d.RestaurantId,
                d.Status,
                d.DeliveryAddress.Street,
                d.DeliveryAddress.City,
                d.DeliveryAddress.BuildingNumber,
                d.DeliveryAddress.Floor,
                d.CreatedAt))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<PendingDeliveryDto>>.Success(dtos);
    }
}
