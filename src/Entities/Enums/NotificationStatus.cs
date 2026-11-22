namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Notification.NotificationStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum NotificationStatus : byte
{
    Pending = 1,
    Processing = 2,
    Sent = 3,
    Delivered = 4,
    Failed = 5,
    Cancelled = 6,
}
