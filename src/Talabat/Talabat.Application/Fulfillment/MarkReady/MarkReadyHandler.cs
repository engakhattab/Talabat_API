using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Fulfillment.MarkReady;

/// <summary>The restaurant marks an order ready for pickup, from Accepted or Preparing (design D2).</summary>
public sealed class MarkReadyHandler
{
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public MarkReadyHandler(
        IOrderFulfillmentRepository fulfillmentRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<ReadyOrder>> Handle(
        MarkReadyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (fulfillment is null)
        {
            return UseCaseResult<ReadyOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.OrderNotFound, "Order was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            fulfillment.MarkReady(command.Actor.ActingRestaurantId(fulfillment), now);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<ReadyOrder>.Failure(DomainExceptionMapper.Map(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<ReadyOrder>.Success(new ReadyOrder(fulfillment.OrderId, now));
    }
}
