using Talabat.Domain.Exceptions;

namespace Talabat.Domain.ValueObjects;

public sealed record Money : IComparable<Money>
{
    public static Money Zero { get; } = new(0m);

    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Money amount cannot be negative.");
        }

        Amount = amount;
    }

    public Money Add(Money other)
    {
        //** Same
        //if (other == null)
        //{
        //    throw new ArgumentNullException(nameof(other));
        //}
        //
        //*//
        ArgumentNullException.ThrowIfNull(other);

        return new Money(checked(Amount + other.Amount));
    }

    public Money Multiply(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidQuantityException();
        }

        return new Money(checked(Amount * quantity));
    }

    public int CompareTo(Money? other)
    {
        return other is null ? 1 : Amount.CompareTo(other.Amount);
    }
}
