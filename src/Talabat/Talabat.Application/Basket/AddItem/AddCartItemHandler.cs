using Talabat.Application.Abstractions;
using Talabat.Application.Basket.Mapping;
using Talabat.Application.Basket.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Aggregates.Basket;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Basket.AddItem;

public sealed class AddCartItemHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IRestaurantRepository _restaurantRepository;
    private readonly IApplicationIdGenerator _idGenerator;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public AddCartItemHandler(
        ICartRepository cartRepository,
        IRestaurantRepository restaurantRepository,
        IApplicationIdGenerator idGenerator,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _restaurantRepository = restaurantRepository ?? throw new ArgumentNullException(nameof(restaurantRepository));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CartDetails>> Handle(
        AddCartItemCommand command,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _restaurantRepository.GetProductSnapshotAsync(
            command.RestaurantId,
            command.ProductId,
            cancellationToken);

        if (snapshot is null)
        {
            return await ResolveMissingProduct(command.RestaurantId, cancellationToken);
        }

        var now = _clock.UtcNow;
        var cart = await _cartRepository.GetActiveCartByCustomerIdAsync(
            command.CustomerId,
            cancellationToken);

        try
        {
            if (cart is null)
            {
                cart = Cart.Create(
                    _idGenerator.NewCartId(),
                    command.CustomerId,
                    snapshot,
                    command.Quantity,
                    now);

                await _cartRepository.AddAsync(cart, cancellationToken);
            }
            else
            {
                cart.AddItem(snapshot, command.Quantity, now);
                _cartRepository.Update(cart);
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

            var details = CartMapper.ToDetails(cart, restaurant);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CartDetails>.Success(details);
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CartDetails>.Failure(DomainExceptionMapper.Map(exception));
        }
    }

    private async Task<UseCaseResult<CartDetails>> ResolveMissingProduct(
        int restaurantId,
        CancellationToken cancellationToken)
    {
        var restaurantExists = await _restaurantRepository.ExistsAsync(restaurantId, cancellationToken);

        return UseCaseResult<CartDetails>.Failure(
            restaurantExists
                ? DomainExceptionMapper.NotFound(ApplicationErrorCodes.ProductNotFound, "Product was not found in this restaurant.")
                : DomainExceptionMapper.NotFound(ApplicationErrorCodes.RestaurantNotFound, "Restaurant was not found."));
    }
}
