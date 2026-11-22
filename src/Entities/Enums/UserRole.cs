namespace GoGoaRentals.Entities.Enums;

/// <summary>
/// Stored in Users.Role, which references the Role table (one row per member, same id and name).
/// Values are persisted; never renumber or reuse a deployed value. Renumbered once by database/007_renumber_roles.sql.
/// </summary>
public enum UserRole : byte
{
    Admin = 1,

    /// <summary>A rental partner: owns vehicles and rents them out through the platform.</summary>
    Partner = 2,

    Customer = 3,
}
