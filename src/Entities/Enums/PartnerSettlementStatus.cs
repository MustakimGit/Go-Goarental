namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in PartnerSettlement.SettlementStatus. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum PartnerSettlementStatus : byte
{
    Pending = 1,
    Approved = 2,
    Paid = 3,
    OnHold = 4,
    Cancelled = 5,
}
