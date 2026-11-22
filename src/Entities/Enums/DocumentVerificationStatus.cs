namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in PartnerVehicle.DocumentVerificationStatus, VehicleDocument.VerificationStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum DocumentVerificationStatus : byte
{
    Pending = 1,
    Verified = 2,
    Rejected = 3,
    Expired = 4,
}
