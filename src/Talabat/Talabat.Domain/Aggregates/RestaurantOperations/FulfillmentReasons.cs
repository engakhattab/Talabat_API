using Talabat.Domain.Exceptions;

namespace Talabat.Domain.Aggregates.RestaurantOperations;

/// <summary>
/// The reason-code lists per action and the note rules (design D13). Restaurant and Admin cancellation lists
/// arrive with their actions (T6); the system list with the acceptance-deadline job (Phase 11).
/// </summary>
internal static class FulfillmentReasons
{
    public const int MaxNoteLength = 500;

    public static readonly IReadOnlySet<FulfillmentReasonCode> Reject = new HashSet<FulfillmentReasonCode>
    {
        FulfillmentReasonCode.ItemsUnavailable,
        FulfillmentReasonCode.RestaurantBusy,
        FulfillmentReasonCode.RestaurantClosing,
        FulfillmentReasonCode.Other
    };

    public static readonly IReadOnlySet<FulfillmentReasonCode> CustomerCancel = new HashSet<FulfillmentReasonCode>
    {
        FulfillmentReasonCode.ChangedMind,
        FulfillmentReasonCode.OrderedByMistake,
        FulfillmentReasonCode.Other
    };

    /// <summary>Checks the code against its action's list and returns the trimmed note (null when blank).</summary>
    public static string? Validate(
        FulfillmentReasonCode? reasonCode,
        string? note,
        IReadOnlySet<FulfillmentReasonCode> allowedCodes)
    {
        if (reasonCode is { } code && !allowedCodes.Contains(code))
        {
            throw new ReasonCodeNotAllowedException(code);
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (reasonCode == FulfillmentReasonCode.Other && trimmedNote is null)
        {
            throw new ReasonNoteRequiredException();
        }

        if (trimmedNote is { Length: > MaxNoteLength })
        {
            throw new ReasonNoteTooLongException(MaxNoteLength);
        }

        return trimmedNote;
    }
}
