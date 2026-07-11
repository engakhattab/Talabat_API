namespace Talabat.Application.Common.Results;

public sealed class UseCaseResult<T>
{
    private readonly T? _value;

    private UseCaseResult(T value)
    {
        IsSuccess = true;
        _value = value;
    }

    private UseCaseResult(ApplicationError error)
    {
        IsSuccess = false;
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public ApplicationError? Error { get; }

    public static UseCaseResult<T> Success(T value)
    {
        return new UseCaseResult<T>(value);
    }

    public static UseCaseResult<T> Failure(ApplicationError error)
    {
        return new UseCaseResult<T>(error);
    }
}
