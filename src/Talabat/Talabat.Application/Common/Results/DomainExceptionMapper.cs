using Talabat.Domain.Exceptions;

namespace Talabat.Application.Common.Results;

public static class DomainExceptionMapper
{
    public static ApplicationError Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            AddressNotFoundException ex => NotFound(ApplicationErrorCodes.AddressNotFound, ex.Message),
            CartExpiredException ex => Conflict(ApplicationErrorCodes.CartExpired, ex.Message),
            CartItemNotFoundException ex => NotFound(ApplicationErrorCodes.CartItemNotFound, ex.Message),
            CartNotActiveException ex => Conflict(ApplicationErrorCodes.CartNotActive, ex.Message),
            CrossRestaurantCartException ex => Conflict(ApplicationErrorCodes.CrossRestaurantCart, ex.Message),
            CurrentProductPriceMissingException ex => Conflict(ApplicationErrorCodes.CurrentProductPriceMissing, ex.Message),
            DuplicateAddressException ex => Conflict(ApplicationErrorCodes.DuplicateAddress, ex.Message),
            EmptyCartCheckoutException ex => Validation(ApplicationErrorCodes.EmptyCart, ex.Message),
            InvalidQuantityException ex => Validation(ApplicationErrorCodes.InvalidQuantity, ex.Message),
            MissingDeliveryAddressException ex => NotFound(ApplicationErrorCodes.AddressNotFound, ex.Message),
            ProductNotFoundException ex => NotFound(ApplicationErrorCodes.ProductNotFound, ex.Message),
            ProductUnavailableException ex => Unavailable(ApplicationErrorCodes.ProductUnavailable, ex.Message),
            RestaurantClosedException ex => Unavailable(ApplicationErrorCodes.RestaurantClosed, ex.Message),
            RestaurantInactiveException ex => Unavailable(ApplicationErrorCodes.RestaurantInactive, ex.Message),
            ArgumentOutOfRangeException ex => Validation(ResolveArgumentCode(ex), ex.Message),
            ArgumentException ex => Validation(ResolveArgumentCode(ex), ex.Message),
            DomainException ex => Conflict(ex.GetType().Name, ex.Message),
            _ => Conflict(exception.GetType().Name, exception.Message)
        };
    }

    public static ApplicationError Validation(string code, string message)
    {
        return new ApplicationError(code, ApplicationErrorCategory.Validation, message);
    }

    public static ApplicationError NotFound(string code, string message)
    {
        return new ApplicationError(code, ApplicationErrorCategory.NotFound, message);
    }

    public static ApplicationError Conflict(string code, string message)
    {
        return new ApplicationError(code, ApplicationErrorCategory.Conflict, message);
    }

    public static ApplicationError Unavailable(string code, string message)
    {
        return new ApplicationError(code, ApplicationErrorCategory.Unavailable, message);
    }

    public static ApplicationError OwnershipMismatch(string code, string message)
    {
        return new ApplicationError(code, ApplicationErrorCategory.OwnershipMismatch, message);
    }

    private static string ResolveArgumentCode(ArgumentException exception)
    {
        return exception.ParamName switch
        {
            "fullName" => ApplicationErrorCodes.InvalidCustomerProfile,
            "age" => ApplicationErrorCodes.InvalidCustomerProfile,
            "phoneNumber" => ApplicationErrorCodes.InvalidCustomerProfile,
            "street" => ApplicationErrorCodes.InvalidAddress,
            "city" => ApplicationErrorCodes.InvalidAddress,
            "buildingNumber" => ApplicationErrorCodes.InvalidAddress,
            "floor" => ApplicationErrorCodes.InvalidAddress,
            "quantity" => ApplicationErrorCodes.InvalidQuantity,
            _ => exception.GetType().Name
        };
    }
}
