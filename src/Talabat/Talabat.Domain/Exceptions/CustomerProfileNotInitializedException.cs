namespace Talabat.Domain.Exceptions;

public sealed class CustomerProfileNotInitializedException : DomainException
{
    public CustomerProfileNotInitializedException()
        : base("Customer profile has not been initialized for this user.")
    {
    }
}
