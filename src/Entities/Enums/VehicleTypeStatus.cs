namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in VehicleType.Status. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum VehicleTypeStatus : byte
{
    Active = 1,
    Inactive = 2,
}
