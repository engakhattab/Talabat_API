namespace Talabat.Domain.Exceptions;

public sealed class CurrentProductPriceMissingException : DomainException
{
    public int ProductId { get; }

    public CurrentProductPriceMissingException(int productId)
        : base($"Current price is missing for product {productId}.")
    {
        ProductId = productId;
    }
}
