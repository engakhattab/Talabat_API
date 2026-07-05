namespace Talabat.Domain.Exceptions;

public sealed class MissingDeliveryAddressException : DomainException
{
    public MissingDeliveryAddressException()
        : base("Checkout requires a delivery address.")
    {
    }
}
