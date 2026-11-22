namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in NotificationAttempt.Result. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum NotificationAttemptResult : byte
{
    Success = 1,
    TransientFailure = 2,
    PermanentFailure = 3,
}
