namespace Talabat.Domain.Exceptions;

public sealed class ReasonCodeRequiredException : DomainException
{
    public ReasonCodeRequiredException()
        : base("A reason code is required for this action.")
    {
    }
}
