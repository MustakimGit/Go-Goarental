namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in VehicleCalendarBlock.BlockType. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum CalendarBlockType : byte
{
    Reservation = 1,
    Maintenance = 2,
    PartnerUnavailable = 3,
}
