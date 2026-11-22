namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in VehicleCalendarBlock.Status (applies to all block types). Values are persisted; never renumber or reuse a deployed value.</summary>
public enum ReservationStatus : byte
{
    Active = 1,
    Expired = 2,
    Released = 3,
    Consumed = 4,
}
