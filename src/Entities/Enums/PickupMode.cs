namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Booking.PickupMode. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum PickupMode : byte
{
    SelfPickup = 1,
    Delivery = 2,
}
