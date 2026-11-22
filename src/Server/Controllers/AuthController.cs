using System.Security.Claims;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    public const string RentalPartnerIdClaim = "rental_partner_id";

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> RegisterCustomer(RegisterCustomerRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterCustomerAsync(
            new RegisterCustomerCommand(request.Name, request.Email, request.Phone, request.Password), cancellationToken);
        await SignInAsync(user);
        return CurrentUserResponse.From(user);
    }

    [HttpPost("register-partner")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> RegisterPartner(RegisterPartnerRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.RegisterPartnerAsync(
            new RegisterPartnerCommand(request.Name, request.Email, request.Phone, request.Password, request.BusinessName, request.Address),
            cancellationToken);
        await SignInAsync(user);
        return CurrentUserResponse.From(user);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<CurrentUserResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await authService.ValidateCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
        {
            return Problem("Email or password is incorrect.", statusCode: StatusCodes.Status401Unauthorized, title: "Login failed");
        }

        await SignInAsync(user);
        return CurrentUserResponse.From(user);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken cancellationToken)
    {
        // Reload from the database so suspensions and partner approvals show up without a new login.
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await authService.GetActiveUserAsync(userId, cancellationToken);
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized();
        }

        return CurrentUserResponse.From(user);
    }

    private Task SignInAsync(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
        };
        if (user.RentalPartnerId is int partnerId)
        {
            claims.Add(new Claim(RentalPartnerIdClaim, partnerId.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
