namespace Talabat.Domain.ValueObjects;

public sealed record TimeRange
{
    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public TimeRange(TimeOnly start, TimeOnly end)
    {
        if (start == end)
        {
            throw new ArgumentException(
                "Start and end times cannot be equal in MVP v1.",
                nameof(end));
        }

        Start = start;
        End = end;
    }

    public bool Contains(TimeOnly time)
    {
        if (Start < End)
        {
            return time >= Start && time < End;
        }

        return time >= Start || time < End;
    }
}
