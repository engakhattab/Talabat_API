namespace Talabat.Domain.Exceptions;

public sealed class ReasonNoteTooLongException : DomainException
{
    public ReasonNoteTooLongException(int maxLength)
        : base($"The reason note cannot be longer than {maxLength} characters.")
    {
    }
}
