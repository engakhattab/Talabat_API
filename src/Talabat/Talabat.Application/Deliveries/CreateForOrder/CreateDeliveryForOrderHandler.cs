using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Interfaces;
using Talabat.Domain.ValueObjects;

namespace Talabat.Application.Deliveries.CreateForOrder;

public sealed class CreateDeliveryForOrderHandler
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public CreateDeliveryForOrderHandler(
        IDeliveryRepository deliveryRepository,
        IRestaurantRepository restaurantRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _deliveryRepository = deliveryRepository ?? throw new ArgumentNullException(nameof(deliveryRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
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

        var restaurant = await _restaurantRepository.GetByIdAsync(
            cmd.RestaurantId,
            cancellationToken);

        if (restaurant is null)
        {
            return UseCaseResult<int>.Failure(
                DomainExceptionMapper.NotFound(
                    ApplicationErrorCodes.RestaurantNotFound,
                    "Restaurant was not found."));
        }

        var pickupAddress = new DeliveryAddressSnapshot(
            restaurant.PickupAddress.Street,
            restaurant.PickupAddress.City,
            restaurant.PickupAddress.BuildingNumber,
            restaurant.PickupAddress.Floor);

        var delivery = new Domain.Aggregates.DeliveryManagement.Delivery(
            cmd.OrderId,
            cmd.CustomerId,
            cmd.RestaurantId,
            restaurant.Name,
            pickupAddress,
            cmd.DeliveryAddress,
            _clock.UtcNow);

        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult<int>.Success(delivery.Id);
    }
}
