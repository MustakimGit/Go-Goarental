using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Server.Infrastructure;

/// <summary>Role names as they appear in the auth cookie's role claim, for [Authorize(Roles = ...)].</summary>
public static class Roles
{
    public const string Customer = nameof(UserRole.Customer);
    public const string Partner = nameof(UserRole.Partner);
    public const string Admin = nameof(UserRole.Admin);
}
