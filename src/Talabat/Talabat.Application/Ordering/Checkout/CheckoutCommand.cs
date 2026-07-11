namespace Talabat.Application.Ordering.Checkout;

public sealed record CheckoutCommand(int CustomerId, int DeliveryAddressId);
