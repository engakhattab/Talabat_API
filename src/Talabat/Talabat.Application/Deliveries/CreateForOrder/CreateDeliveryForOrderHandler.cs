using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Deliveries.CreateForOrder;

public sealed class CreateDeliveryForOrderHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateDeliveryForOrderHandler(
        IDeliveryRepository deliveryRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<int>> Handle(
        CreateDeliveryForOrderCommand cmd,
        CancellationToken cancellationToken = default)
    {
        var existing = await _deliveryRepository.GetByOrderIdAsync(cmd.OrderId, cancellationToken);
        if (existing is not null)
        {
            return UseCaseResult<int>.Failure(
                new ApplicationError(
                    ApplicationErrorCodes.DeliveryAlreadyExists,
                    ApplicationErrorCategory.Conflict,
                    "A delivery already exists for this order."));
        }

        var delivery = new Domain.Aggregates.DeliveryManagement.Delivery(
            cmd.OrderId,
            cmd.CustomerId,
            cmd.RestaurantId,
            cmd.DeliveryAddress,
            _clock.UtcNow);

        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<int>.Success(delivery.Id);
    }
}
