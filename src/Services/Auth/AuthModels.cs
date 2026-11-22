using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Services.Common;

namespace GoGoaRentals.Services.Auth;

public sealed record AuthenticatedUser(
    int UserId,
    string Name,
    string Email,
    string? Phone,
    UserRole Role,
    int? RentalPartnerId,
    string? BusinessName,
    PartnerStatus? PartnerStatus);

public sealed record RegisterCustomerCommand(string Name, string Email, string? Phone, string Password);

public sealed record RegisterPartnerCommand(
    string Name,
    string Email,
    string Phone,
    string Password,
    string BusinessName,
    string? Address);

public sealed class EmailAlreadyRegisteredException()
    : ConflictException("An account with this email or phone number already exists.");
