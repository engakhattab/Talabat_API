using Talabat.Application.Abstractions;
using Talabat.Application.Basket.Models;
using Talabat.Application.Common.Results;
using Talabat.Domain.Exceptions;
using Talabat.Domain.Interfaces;

namespace Talabat.Application.Basket.ClearCart;

public sealed class ClearCartHandler
{
    private readonly ICartRepository _cartRepository;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ClearCartHandler(
        ICartRepository cartRepository,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UseCaseResult<CartDetails>> Handle(
        ClearCartCommand command,
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
            cart.Clear(_clock.UtcNow);
            _cartRepository.Update(cart);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return UseCaseResult<CartDetails>.Success(CartDetails.Empty(command.CustomerId));
        }
        catch (Exception exception) when (exception is DomainException or ArgumentException)
        {
            return UseCaseResult<CartDetails>.Failure(DomainExceptionMapper.Map(exception));
        }
    }
}
