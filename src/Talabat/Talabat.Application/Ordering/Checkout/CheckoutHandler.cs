using Talabat.Application.Abstractions;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Ordering;
using Talabat.Domain.DomainServices.Checkout;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Ordering.Checkout;

public sealed class CheckoutHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IRestaurantLocalTimeProvider _restaurantLocalTimeProvider;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CheckoutDomainService _checkoutDomainService;

    public CheckoutHandler(
        ICartRepository cartRepository,
        IUserRepository userRepository,
        IRestaurantRepository restaurantRepository,
        IOrderRepository orderRepository,
        IRestaurantLocalTimeProvider restaurantLocalTimeProvider,
        IClock clock,
        IUnitOfWork unitOfWork,
        CheckoutDomainService checkoutDomainService)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _restaurantLocalTimeProvider = restaurantLocalTimeProvider ?? throw new ArgumentNullException(nameof(restaurantLocalTimeProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _checkoutDomainService = checkoutDomainService ?? throw new ArgumentNullException(nameof(checkoutDomainService));
    }

    public async Task<UseCaseResult<CheckoutOutcome>> Handle(
        CheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var cart = await _cartRepository.GetActiveCartByCustomerIdAsync(
            command.CustomerId,
            cancellationToken);

        if (cart is null)
        {
            return UseCaseResult<CheckoutOutcome>.Failure(CheckoutErrors.CartNotFound());
        }

        var user = await _userRepository.GetByIdWithAddressesAsync(
            command.CustomerId,
            cancellationToken);

        if (user is null)
        {
            return UseCaseResult<CheckoutOutcome>.Failure(CheckoutErrors.CustomerNotFound());
        }

        try
        {
            var deliveryAddress = user.CreateDeliveryAddressSnapshot(command.DeliveryAddressId);

            var restaurant = await _restaurantRepository.GetByIdWithProductsAsync(
                cart.RestaurantId,
                cancellationToken);

            if (restaurant is null)
            {
                return UseCaseResult<CheckoutOutcome>.Failure(CheckoutErrors.RestaurantNotFound());
            }

            var restaurantLocalTime = _restaurantLocalTimeProvider.GetLocalTime(restaurant, now);
            var checkoutResult = _checkoutDomainService.ValidateCheckout(
                cart,
                restaurant,
                deliveryAddress,
                now,
                restaurantLocalTime);

            if (checkoutResult is CheckoutProductsUnavailable unavailable)
            {
                return UseCaseResult<CheckoutOutcome>.Success(
                    CheckoutResultMapper.ToOutcome(unavailable));
            }

            var checkoutSucceeded = (CheckoutSucceeded)checkoutResult;
            var order = Order.CreateFromCheckout(
                command.CustomerId,
                cart.RestaurantId,
                checkoutSucceeded.Items,
                deliveryAddress,
                now);

            await _orderRepository.AddAsync(order, cancellationToken);
            cart.MarkCheckedOut(now);
            _cartRepository.Update(cart);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CheckoutOutcome>.Success(
                CheckoutResultMapper.ToOutcome(order.Id, checkoutSucceeded));
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CheckoutOutcome>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
