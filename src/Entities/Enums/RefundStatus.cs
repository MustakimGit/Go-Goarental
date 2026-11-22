namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Refund.RefundStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum RefundStatus : byte
{
    Requested = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
}
