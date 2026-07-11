using Talabat.Application.Abstractions;
using Talabat.Application.Basket.Mapping;
using Talabat.Application.Basket.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Basket.RemoveItem;

public sealed class RemoveCartItemHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveCartItemHandler(
        ICartRepository cartRepository,
        IRestaurantRepository restaurantRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CartDetails>> Handle(
        RemoveCartItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetActiveCartByCustomerIdAsync(
            command.CustomerId,
            cancellationToken);

        if (cart is null)
        {
            return UseCaseResult<CartDetails>.Failure(
                DomainExceptionMapper.NotFound(ApplicationErrorCodes.CartNotFound, "Cart was not found."));
        }

        try
        {
            cart.RemoveItem(command.ProductId, _clock.UtcNow);

            var restaurant = await _restaurantRepository.GetByIdWithProductsAsync(
                cart.RestaurantId,
                cancellationToken);

            if (restaurant is null)
            {
                return UseCaseResult<CartDetails>.Failure(
                    DomainExceptionMapper.NotFound(
                        ApplicationErrorCodes.RestaurantNotFound,
                        "Restaurant was not found."));
            }

            var details = CartMapper.ToDetails(cart, restaurant);
            _cartRepository.Update(cart);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CartDetails>.Success(details);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CartDetails>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
