using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.CancelOrder;

/// <summary>
/// The customer cancels their own order while the restaurant has not accepted it (design D7, Q2). Another
/// customer's order is reported as not found.
/// </summary>
public sealed class CancelOrderHandler
{
    private readonly IOrderRepository _orderRepository;
    private readonly IOrderFulfillmentRepository _fulfillmentRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelOrderHandler(
        IOrderRepository orderRepository,
        IOrderFulfillmentRepository fulfillmentRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _fulfillmentRepository = fulfillmentRepository ?? throw new ArgumentNullException(nameof(fulfillmentRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CancelledOrder>> Handle(
        CancelOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var order = await _orderRepository.GetByIdForCustomerAsync(
            command.OrderId,
            command.CustomerId,
            cancellationToken);
        var fulfillment = order is null
            ? null
            : await _fulfillmentRepository.GetByOrderIdAsync(order.Id, cancellationToken);

        if (fulfillment is null)
        {
            return UseCaseResult<CancelledOrder>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.OrderNotFound, "Order was not found."));
        }

        var now = _clock.UtcNow;

        try
        {
            fulfillment.CancelByCustomer(command.ReasonCode, command.Note, now);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CancelledOrder>.Failure(DomainExceptionMapper.Map(exception));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<CancelledOrder>.Success(new CancelledOrder(fulfillment.OrderId, command.ReasonCode, now));
    }
}
