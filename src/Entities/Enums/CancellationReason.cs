namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Booking.CancellationReason. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum CancellationReason : byte
{
    CustomerRequested = 1,
    PaymentNotCompleted = 2,
    NoVehicleAvailable = 3,
    PartnerUnableToFulfil = 4,
    AdminCancelled = 5,
    CustomerNoShow = 6,
}
