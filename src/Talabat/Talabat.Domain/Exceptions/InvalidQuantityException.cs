namespace Talabat.Domain.Exceptions;

public sealed class InvalidQuantityException : DomainException
{
    public InvalidQuantityException()
        : base("Quantity must be greater than zero.")
    {
    }
}
