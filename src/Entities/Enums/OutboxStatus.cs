namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in NotificationOutbox.ProcessingStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum OutboxStatus : byte
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
}
