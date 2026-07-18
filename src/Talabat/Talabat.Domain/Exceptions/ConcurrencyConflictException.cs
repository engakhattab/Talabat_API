namespace Talabat.Domain.Exceptions;

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException()
        : base("The record has been modified by another process. Please retry.")
    {
    }
}
