using Talabat.Application.Common.Results;

namespace Talabat.Application.Ordering.Checkout;

public static class CheckoutErrors
{
    public static ApplicationError CartNotFound()
    {
        return DomainExceptionMapper.NotFound(
            ApplicationErrorCodes.CartNotFound,
            "Cart was not found.");
    }

    public static ApplicationError CustomerNotFound()
    {
        return DomainExceptionMapper.NotFound(
            ApplicationErrorCodes.CustomerNotFound,
            "Customer profile was not found.");
    }

    public static ApplicationError RestaurantNotFound()
    {
        return DomainExceptionMapper.NotFound(
            ApplicationErrorCodes.RestaurantNotFound,
            "Restaurant was not found.");
    }
}
