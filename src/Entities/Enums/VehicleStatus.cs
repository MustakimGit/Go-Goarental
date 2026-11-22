namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in PartnerVehicle.Status. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum VehicleStatus : byte
{
    PendingVerification = 1,
    Active = 2,
    Inactive = 3,
    Retired = 4,
}
