namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in RentalPartner.PartnerStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum PartnerStatus : byte
{
    PendingApproval = 1,
    Active = 2,
    Suspended = 3,
    Rejected = 4,
}
