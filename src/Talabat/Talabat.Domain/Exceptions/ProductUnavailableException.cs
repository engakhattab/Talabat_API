namespace Talabat.Domain.Exceptions;

public sealed class ProductUnavailableException : DomainException
{
    public ProductUnavailableException()
        : base("This product is currently unavailable.")
    {
    }
}
