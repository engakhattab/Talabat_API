namespace Talabat.Domain.Exceptions;

public sealed class DuplicateProductException : DomainException
{
    public DuplicateProductException()
        : base("This product already exists in the restaurant.")
    {
    }
}
