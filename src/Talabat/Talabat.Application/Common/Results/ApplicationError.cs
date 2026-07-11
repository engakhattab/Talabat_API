namespace Talabat.Application.Common.Results;

public sealed record ApplicationError
{
    public ApplicationError(
        string code,
        ApplicationErrorCategory category,
        string message)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Application error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Application error message is required.", nameof(message));
        }

        Code = code.Trim();
        Category = category;
        Message = message.Trim();
    }

    public string Code { get; }

    public ApplicationErrorCategory Category { get; }

    public string Message { get; }
}
