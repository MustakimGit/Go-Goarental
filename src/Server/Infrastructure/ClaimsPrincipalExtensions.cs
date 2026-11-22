using System.Security.Claims;
using GoGoaRentals.Server.Controllers;

namespace GoGoaRentals.Server.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("No user id claim; is the endpoint missing [Authorize]?"));

    public static int GetRentalPartnerId(this ClaimsPrincipal user)
        => int.Parse(user.FindFirstValue(AuthController.RentalPartnerIdClaim)
                     ?? throw new InvalidOperationException("No partner id claim; is the endpoint missing the partner role check?"));
}
