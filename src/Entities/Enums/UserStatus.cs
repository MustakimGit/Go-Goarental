namespace GoGoaRentals.Entities.Enums;

/// <summary>Stored in Users.Status. Values are persisted; never renumber or reuse a deployed value.</summary>
public enum UserStatus : byte
{
    Active = 1,
    Suspended = 2,
    Deleted = 3,
}
