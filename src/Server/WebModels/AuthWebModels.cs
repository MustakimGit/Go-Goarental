using System.ComponentModel.DataAnnotations;
using GoGoaRentals.Services.Auth;

namespace GoGoaRentals.Server.WebModels;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, MaxLength(128)]
    public string Password { get; set; } = "";
}

public sealed class RegisterCustomerRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Phone, MaxLength(20)]
    public string? Phone { get; set; }

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";
}

public sealed class RegisterPartnerRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [Required, Phone, MaxLength(20)]
    public string Phone { get; set; } = "";

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";

    [Required, MaxLength(200)]
    public string BusinessName { get; set; } = "";

    [MaxLength(500)]
    public string? Address { get; set; }
}

public sealed record CurrentUserResponse(
    int UserId,
    string Name,
    string Email,
    string? Phone,
    string Role,
    int? RentalPartnerId,
    string? BusinessName,
    string? PartnerStatus)
{
    public static CurrentUserResponse From(AuthenticatedUser user) => new(
        user.UserId,
        user.Name,
        user.Email,
        user.Phone,
        user.Role.ToString(),
        user.RentalPartnerId,
        user.BusinessName,
        user.PartnerStatus?.ToString());
}
