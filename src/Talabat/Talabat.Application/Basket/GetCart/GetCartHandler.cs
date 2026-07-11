using Talabat.Application.Abstractions;
using Talabat.Application.Basket.Mapping;
using Talabat.Application.Basket.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Basket.GetCart;

public sealed class GetCartHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IClock _clock;

    public GetCartHandler(
        ICartRepository cartRepository,
        IRestaurantRepository restaurantRepository,
        IClock clock)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<UseCaseResult<CartDetails>> Handle(
        GetCartQuery query,
        CancellationToken cancellationToken = default)
    {
        var cart = await _cartRepository.GetActiveCartByCustomerIdAsync(
            query.CustomerId,
            cancellationToken);

        if (cart is null)
        {
            return UseCaseResult<CartDetails>.Success(CartDetails.Empty(query.CustomerId));
        }

        if (cart.IsExpired(_clock.UtcNow))
        {
            return UseCaseResult<CartDetails>.Failure(
                DomainExceptionMapper.Map(new CartExpiredException()));
        }

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

        try
        {
            return UseCaseResult<CartDetails>.Success(CartMapper.ToDetails(cart, restaurant));
        }
        catch (DomainException exception)
        {
            return UseCaseResult<CartDetails>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
