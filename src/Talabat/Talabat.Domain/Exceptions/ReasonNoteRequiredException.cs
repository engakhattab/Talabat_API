namespace Talabat.Domain.Exceptions;

public sealed class ReasonNoteRequiredException : DomainException
{
    public ReasonNoteRequiredException()
        : base("A note is required when the reason code is 'Other'.")
    {
    }
}
