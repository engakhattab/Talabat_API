namespace Talabat.Application.Abstractions;

public interface ICurrentUser
{
    string IdentityUserId { get; }

    bool IsAuthenticated { get; }

    bool HasProfile { get; }

    int? CustomerId { get; }
}
