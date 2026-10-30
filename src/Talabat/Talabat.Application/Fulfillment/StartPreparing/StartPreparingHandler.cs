using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Fulfillment.StartPreparing;

/// <summary>The kitchen starts preparing an accepted order (design D2).</summary>
public sealed class StartPreparingHandler
{
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public StartPreparingHandler(
        IOrderFulfillmentRepository fulfillmentRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<PreparingOrder>> Handle(
        StartPreparingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var fulfillment = await _fulfillmentRepository.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (fulfillment is null)
        {
            return UseCaseResult<PreparingOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.OrderNotFound, "Order was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            fulfillment.StartPreparing(command.Actor.ActingRestaurantId(fulfillment), now);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<PreparingOrder>.Failure(DomainExceptionMapper.Map(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<PreparingOrder>.Success(new PreparingOrder(fulfillment.OrderId, now));
    }
}
