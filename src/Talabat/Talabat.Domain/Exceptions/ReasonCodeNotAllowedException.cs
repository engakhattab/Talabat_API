using Talabat.Domain.Aggregates.RestaurantOperations;

namespace Talabat.Domain.Exceptions;

public sealed class ReasonCodeNotAllowedException : DomainException
{
    public ReasonCodeNotAllowedException(FulfillmentReasonCode reasonCode)
        : base($"Reason code '{reasonCode}' is not allowed for this action.")
    {
    }
}
