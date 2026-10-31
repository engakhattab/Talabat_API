using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Fulfillment.RejectOrder;

/// <summary>The restaurant declines an order it has not accepted, with a reason code (design D2, D13).</summary>
public sealed class RejectOrderHandler
{
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RejectOrderHandler(
        IOrderFulfillmentRepository fulfillmentRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<RejectedOrder>> Handle(
        RejectOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (fulfillment is null)
        {
            return UseCaseResult<RejectedOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.OrderNotFound, "Order was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            fulfillment.Reject(
                command.Actor.ActingRestaurantId(fulfillment),
                command.ReasonCode,
                command.Note,
                now);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<RejectedOrder>.Failure(DomainExceptionMapper.Map(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<RejectedOrder>.Success(new RejectedOrder(fulfillment.OrderId, command.ReasonCode, now));
    }
}
