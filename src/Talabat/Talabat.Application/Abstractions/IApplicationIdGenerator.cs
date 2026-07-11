namespace Talabat.Application.Abstractions;

public interface IApplicationIdGenerator
{
    int NewCartId();

    int NewCustomerAddressId();

    int NewOrderId();
}
