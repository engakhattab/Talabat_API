namespace Talabat.Domain.Exceptions;

public sealed class ProductNotFoundException : DomainException
{
    public ProductNotFoundException()
        : base("Product was not found in this restaurant.")
    {
    }
}
