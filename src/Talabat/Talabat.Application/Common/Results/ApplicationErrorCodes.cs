namespace Talabat.Application.Common.Results;

public static class ApplicationErrorCodes
{
    public const string AddressNotFound = nameof(AddressNotFound);
    public const string CartExpired = nameof(CartExpired);
    public const string CartItemNotFound = nameof(CartItemNotFound);
    public const string CartNotActive = nameof(CartNotActive);
    public const string CartNotFound = nameof(CartNotFound);
    public const string CrossRestaurantCart = nameof(CrossRestaurantCart);
    public const string CurrentProductPriceMissing = nameof(CurrentProductPriceMissing);
    public const string CustomerNotFound = nameof(CustomerNotFound);
    public const string DuplicateAddress = nameof(DuplicateAddress);
    public const string EmptyCart = nameof(EmptyCart);
    public const string InvalidAddress = nameof(InvalidAddress);
    public const string InvalidCustomerProfile = nameof(InvalidCustomerProfile);
    public const string InvalidQuantity = nameof(InvalidQuantity);
    public const string OrderNotFound = nameof(OrderNotFound);
    public const string ProductNotFound = nameof(ProductNotFound);
    public const string ProductUnavailable = nameof(ProductUnavailable);
    public const string ProfileAlreadyExists = nameof(ProfileAlreadyExists);
    public const string ProfileNotCreated = nameof(ProfileNotCreated);
    public const string RestaurantClosed = nameof(RestaurantClosed);
    public const string RestaurantInactive = nameof(RestaurantInactive);
    public const string RestaurantNotFound = nameof(RestaurantNotFound);
}
