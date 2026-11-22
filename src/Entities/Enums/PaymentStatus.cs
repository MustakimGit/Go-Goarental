namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Payment.PaymentStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum PaymentStatus : byte
{
    Pending = 1,
    Authorized = 2,
    Paid = 3,
    Failed = 4,
    RefundPending = 5,
    Refunded = 6,
    PartiallyRefunded = 7,
}
