namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in UserNotificationPreference.Channel, Notification.Channel, NotificationTemplate.Channel. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum NotificationChannel : byte
{
    Email = 1,
    Sms = 2,
    WhatsApp = 3,
    InApp = 4,
    WebPush = 5,
}
