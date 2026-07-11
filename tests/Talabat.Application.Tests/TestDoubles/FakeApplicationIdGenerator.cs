using Talabat.Application.Abstractions;

namespace Talabat.Application.Tests.TestDoubles;

public sealed class FakeApplicationIdGenerator : IApplicationIdGenerator
{
    private int _nextCartId = 100;
    private int _nextCustomerAddressId = 200;
    private int _nextOrderId = 300;

    public int NewCartId()
    {
        return _nextCartId++;
    }

    public int NewCustomerAddressId()
    {
        return _nextCustomerAddressId++;
    }

    public int NewOrderId()
    {
        return _nextOrderId++;
    }
}
