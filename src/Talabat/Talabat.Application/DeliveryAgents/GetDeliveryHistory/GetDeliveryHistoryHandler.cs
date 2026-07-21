using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.GetDeliveryHistory;

public sealed class GetDeliveryHistoryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly ICurrentUser _currentUser;

    public GetDeliveryHistoryHandler(
        IDeliveryRepository deliveryRepository,
        ICurrentUser currentUser)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
    }

    public async Task<UseCaseResult<IReadOnlyCollection<DeliveryHistoryDto>>> Handle(
        GetDeliveryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.HasDeliveryAgentCapability || _currentUser.AgentId is null)
        {
            return UseCaseResult<IReadOnlyCollection<DeliveryHistoryDto>>.Failure(
                DomainExceptionMapper.OwnershipMismatch(
                    ApplicationErrorCodes.AgentRequired,
                    "Authenticated delivery agent required."));
        }

        var agentId = _currentUser.AgentId.Value;

        var deliveries = await _deliveryRepository.GetAssignedToAgentAsync(agentId, cancellationToken);

        var dtos = deliveries
            .Select(d => new DeliveryHistoryDto(
                d.Id,
                d.OrderId,
                d.CustomerId,
                d.RestaurantId,
                d.Status,
                d.DeliveryAddress.Street,
                d.DeliveryAddress.City,
                d.DeliveryAddress.BuildingNumber,
                d.DeliveryAddress.Floor,
                d.AssignedAt,
                d.DeliveredAt))
            .ToList()
            .AsReadOnly();

        return UseCaseResult<IReadOnlyCollection<DeliveryHistoryDto>>.Success(dtos);
    }
}
