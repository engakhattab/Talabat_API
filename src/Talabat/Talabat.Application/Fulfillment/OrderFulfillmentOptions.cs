namespace Talabat.Application.Fulfillment;

/// <summary>
/// Order fulfillment settings (design D10), bound and validated by Infrastructure from the
/// <c>OrderFulfillment</c> configuration section.
/// </summary>
public sealed class OrderFulfillmentOptions
{
    public const string SectionName = "OrderFulfillment";

    /// <summary>How long a restaurant has to accept a placed order.</summary>
    public TimeSpan AcceptanceWindow { get; init; } = TimeSpan.FromMinutes(10);
}
