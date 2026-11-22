namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Booking.BookingStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum BookingStatus : byte
{
    Pending = 1,
    AllocationInProgress = 2,
    AwaitingPayment = 3,
    Confirmed = 4,
    InProgress = 5,
    Completed = 6,
    CancellationRequested = 7,
    Cancelled = 8,
    Failed = 9,
}
