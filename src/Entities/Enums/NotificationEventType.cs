namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in NotificationOutbox.EventType, Notification.EventType, NotificationTemplate.EventType (SMALLINT, no CHECK - validated in code). 1xx customer, 2xx partner, 3xx admin. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum NotificationEventType : short
{
    BookingConfirmed = 100,
    PaymentSucceeded = 101,
    PaymentFailed = 102,
    PickupReminder = 103,
    BookingCancelled = 104,
    RefundInitiated = 105,
    RefundCompleted = 106,
    BookingInstructionsChanged = 107,
    PartnerBookingAssigned = 200,
    PartnerBookingCancelled = 201,
    PartnerHandoverReminder = 202,
    PartnerReturnReminder = 203,
    PartnerReassignmentRequest = 204,
    PartnerSettlementUpdated = 205,
    AdminAllocationFailed = 300,
    AdminFulfilmentAtRisk = 301,
    AdminPaymentVerificationFailed = 302,
    AdminRefundFailed = 303,
    AdminNotificationDeliveryFailing = 304,
    AdminSuspiciousPaymentEvent = 305,
}
