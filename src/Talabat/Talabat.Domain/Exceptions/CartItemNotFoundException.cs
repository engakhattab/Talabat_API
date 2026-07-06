namespace Talabat.Domain.Exceptions;

public sealed class CartItemNotFoundException : DomainException
{
    public int ProductId { get; }

    public CartItemNotFoundException(int productId)
        : base($"Cart item for product {productId} was not found.")
    {
        ProductId = productId;
    }
}
