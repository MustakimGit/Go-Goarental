using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Auth;

public sealed class AuthService(AppDbContext db, IPasswordHasher<User> passwordHasher) : IAuthService
{
    private static readonly byte ActiveStatus = EnumValue.ToByte(UserStatus.Active);

    public async Task<AuthenticatedUser> RegisterCustomerAsync(RegisterCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var user = NewUser(command.Name, command.Email, command.Phone, command.Password, UserRole.Customer);
        db.Users.Add(user);
        await SaveNewAccountAsync(cancellationToken);
        return ToAuthenticatedUser(user);
    }

    public async Task<AuthenticatedUser> RegisterPartnerAsync(RegisterPartnerCommand command, CancellationToken cancellationToken = default)
    {
        var user = NewUser(command.Name, command.Email, command.Phone, command.Password, UserRole.Partner);
        user.RentalPartner = new RentalPartner
        {
            BusinessName = command.BusinessName.Trim(),
            ContactPhone = command.Phone.Trim(),
            ContactEmail = user.Email,
            Address = string.IsNullOrWhiteSpace(command.Address) ? null : command.Address.Trim(),
            PartnerStatus = EnumValue.ToByte(PartnerStatus.PendingApproval),
        };
        db.Users.Add(user);
        await SaveNewAccountAsync(cancellationToken);
        return ToAuthenticatedUser(user);
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users
            .Include(u => u.RentalPartner)
            .SingleOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            // Spend the same time hashing so response timing doesn't reveal which emails exist.
            passwordHasher.HashPassword(new User(), password);
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed || user.Status != ActiveStatus)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToAuthenticatedUser(user);
    }

    public async Task<AuthenticatedUser?> GetActiveUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(u => u.RentalPartner)
            .SingleOrDefaultAsync(u => u.UserId == userId && u.Status == ActiveStatus, cancellationToken);
        return user is null ? null : ToAuthenticatedUser(user);
    }

    public async Task EnsureAdminAsync(string name, string email, string password, CancellationToken cancellationToken = default)
    {
        var admin = EnumValue.ToByte(UserRole.Admin);
        if (await db.Users.AnyAsync(u => u.Role == admin, cancellationToken))
        {
            return;
        }

        db.Users.Add(NewUser(name, email, null, password, UserRole.Admin));
        try
        {
            await SaveNewAccountAsync(cancellationToken);
        }
        catch (EmailAlreadyRegisteredException)
        {
            // Another app instance (e.g. a parallel test host) seeded it first.
        }
    }

    private User NewUser(string name, string email, string? phone, string password, UserRole role)
    {
        var now = DateTime.UtcNow;
        var user = new User
        {
            Name = name.Trim(),
            Email = NormalizeEmail(email),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            Role = EnumValue.ToByte(role),
            Status = ActiveStatus,
            CreatedAt = now,
            UpdatedAt = now,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        return user;
    }

    private async Task SaveNewAccountAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsDuplicateKey(ex))
        {
            db.ChangeTracker.Clear();
            throw new EmailAlreadyRegisteredException();
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AuthenticatedUser ToAuthenticatedUser(User user) => new(
        user.UserId,
        user.Name,
        user.Email,
        user.Phone,
        EnumValue.Parse<UserRole>(user.Role),
        user.RentalPartner?.RentalPartnerId,
        user.RentalPartner?.BusinessName,
        user.RentalPartner is null ? null : EnumValue.Parse<PartnerStatus>(user.RentalPartner.PartnerStatus));
}
