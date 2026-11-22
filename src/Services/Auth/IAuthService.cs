namespace GoGoaRentals.Services.Auth;

public interface IAuthService
{
    Task<AuthenticatedUser> RegisterCustomerAsync(RegisterCustomerCommand command, CancellationToken cancellationToken = default);

    /// <summary>Creates the partner's login and a RentalPartner row awaiting admin approval.</summary>
    Task<AuthenticatedUser> RegisterPartnerAsync(RegisterPartnerCommand command, CancellationToken cancellationToken = default);

    /// <summary>Returns null for an unknown email, wrong password or an inactive account; callers must not say which.</summary>
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns null if the user no longer exists or is not active.</summary>
    Task<AuthenticatedUser?> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Development only: creates the super admin if no super admin exists yet.</summary>
    Task EnsureAdminAsync(string name, string email, string password, CancellationToken cancellationToken = default);
}
