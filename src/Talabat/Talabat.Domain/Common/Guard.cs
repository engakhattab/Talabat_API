namespace Talabat.Domain.Common;

internal static class Guard
{
    public static int Positive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"{parameterName} must be greater than zero.");
        }

        return value;
    }

    public static string RequiredText(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                $"{parameterName} is required and cannot be empty.",
                parameterName);
        }

        return value.Trim();
    }

    public static string? OptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
