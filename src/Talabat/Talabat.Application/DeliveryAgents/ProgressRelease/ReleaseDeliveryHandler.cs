using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.DeliveryManagement;
using Talabat.Domain.DomainServices.DeliveryManagement;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.DeliveryAgents.ProgressRelease;

/// <summary>
/// Release (design D6, BR-DEL-017): the delivery goes back to the queue on the same row and the agent becomes
/// Available. The order and its fulfillment are untouched.
/// </summary>
public sealed class ReleaseDeliveryHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUserRepository _userRepository;
    private readonly DeliveryAssignmentDomainService _domainService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReleaseDeliveryHandler(
        IDeliveryRepository deliveryRepository,
        IUserRepository userRepository,
        DeliveryAssignmentDomainService domainService,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _domainService = domainService ?? throw new ArgumentNullException(nameof(domainService));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<ReleasedDelivery>> Handle(
        ReleaseDeliveryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var delivery = await FindDeliveryAsync(command, cancellationToken);

        if (delivery is null)
        {
            return UseCaseResult<ReleasedDelivery>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.DeliveryNotFound,
                    "Delivery was not found."));
        }

        if (delivery.AssignedAgentId is not { } agentId)
        {
            return UseCaseResult<ReleasedDelivery>.Failure(
                DomainExceptionMapper.Map(new DeliveryNotAssignedException()));
        }

        var agent = await _userRepository.GetByIdAsync(agentId, cancellationToken);

        if (agent is null)
        {
            return UseCaseResult<ReleasedDelivery>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.UserNotFound,
                    "Agent was not found."));
        }

        try
        {
            _domainService.ReleaseDelivery(delivery, agent, _clock.UtcNow);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<ReleasedDelivery>.Failure(DomainExceptionMapper.Map(exception));
        }

        // Delivery and agent are saved together in one SaveChanges, so they commit atomically.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<ReleasedDelivery>.Success(
            new ReleasedDelivery(delivery.Id, delivery.OrderId, agentId));
    }

    private Task<Delivery?> FindDeliveryAsync(ReleaseDeliveryCommand command, CancellationToken cancellationToken)
    {
        return command switch
        {
            ReleaseDeliveryCommand.ByAgent byAgent =>
                _deliveryRepository.GetByIdForAgentAsync(byAgent.DeliveryId, byAgent.AgentId, cancellationToken),
            ReleaseDeliveryCommand.ByAdmin byAdmin =>
                _deliveryRepository.GetByOrderIdAsync(byAdmin.OrderId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        };
    }
}
