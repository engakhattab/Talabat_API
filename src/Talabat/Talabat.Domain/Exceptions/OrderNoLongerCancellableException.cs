namespace Talabat.Domain.Exceptions;

public sealed class OrderNoLongerCancellableException : DomainException
{
    public OrderNoLongerCancellableException()
        : base("The order can no longer be cancelled.")
    {
    }
}
