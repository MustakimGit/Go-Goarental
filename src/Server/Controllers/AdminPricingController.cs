using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/admin/prices")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminPricingController(IPricingAdminService pricing) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<DailyPriceDto>> List([FromQuery] int? vehicleTypeId, CancellationToken cancellationToken)
        => pricing.ListAsync(vehicleTypeId, cancellationToken);

    [HttpPost]
    public Task<DailyPriceDto> Create(CreateDailyPriceRequest request, CancellationToken cancellationToken)
        => pricing.CreateAsync(
            new CreateDailyPriceCommand(request.VehicleTypeId, request.DailyPrice, request.DepositAmount, request.TaxPercent, request.FirstDay!.Value, request.LastDay),
            User.GetUserId(),
            cancellationToken);

    [HttpDelete("{id:int}")]
    public Task<DailyPriceDto> Withdraw(int id, CancellationToken cancellationToken)
        => pricing.DeactivateAsync(id, User.GetUserId(), cancellationToken);
}
