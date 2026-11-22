using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Services.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminDashboardController(IAdminDashboardService dashboard) : ControllerBase
{
    /// <summary>Totals plus every partner with their vehicles and whether each is bookable right now.</summary>
    [HttpGet]
    public Task<AdminDashboard> Get(CancellationToken cancellationToken)
        => dashboard.GetAsync(cancellationToken);
}
