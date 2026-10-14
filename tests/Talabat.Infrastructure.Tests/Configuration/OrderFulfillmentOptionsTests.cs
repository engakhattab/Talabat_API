using Microsoft.Extensions.Configuration;
using Talabat.Application.Fulfillment;

namespace Talabat.Infrastructure.Tests.Configuration;

public sealed class OrderFulfillmentOptionsTests
{
    [Fact]
    public void Acceptance_window_defaults_to_ten_minutes_and_binds_from_configuration()
    {
        var defaults = Build(acceptanceWindow: null).GetRequiredService<OrderFulfillmentOptions>();
        var configured = Build(acceptanceWindow: "00:15:00").GetRequiredService<OrderFulfillmentOptions>();

        Assert.Equal(TimeSpan.FromMinutes(10), defaults.AcceptanceWindow);
        Assert.Equal(TimeSpan.FromMinutes(15), configured.AcceptanceWindow);
    }

    // A misconfigured window must stop the host at startup, not surface as a checkout failure.
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:05:00")]
    public void Non_positive_acceptance_window_is_rejected_at_startup(string acceptanceWindow)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build(acceptanceWindow));

        Assert.Contains("OrderFulfillment:AcceptanceWindow", exception.Message);
    }

    private static ServiceProvider Build(string? acceptanceWindow)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:TalabatDb"] = "Server=(localdb)\\MSSQLLocalDB;Database=unused"
        };

        if (acceptanceWindow is not null)
        {
            settings["OrderFulfillment:AcceptanceWindow"] = acceptanceWindow;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddInfrastructure(configuration).BuildServiceProvider();
    }
}
