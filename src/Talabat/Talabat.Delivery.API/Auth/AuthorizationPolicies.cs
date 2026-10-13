namespace Talabat.Delivery.API.Auth;

public static class AuthorizationPolicies
{
    public const string DeliveryApplicantAccess = nameof(DeliveryApplicantAccess);
    public const string DeliveryAgentAccess = nameof(DeliveryAgentAccess);
    public const string OperationalDeliveryAgentAccess = nameof(OperationalDeliveryAgentAccess);
}
