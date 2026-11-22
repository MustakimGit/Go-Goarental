using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Partners;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/admin/partners")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminPartnersController(IPartnerAdminService partners) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<PartnerSummary>> List([FromQuery] PartnerStatus? status, CancellationToken cancellationToken)
        => partners.ListAsync(status, cancellationToken);

    [HttpPut("{id:int}/status")]
    public Task<PartnerSummary> ChangeStatus(int id, ChangePartnerStatusRequest request, CancellationToken cancellationToken)
        => partners.ChangeStatusAsync(id, request.Status!.Value, request.Reason, User.GetUserId(), cancellationToken);

    [HttpPut("{id:int}/commission")]
    public Task<PartnerSummary> SetCommission(int id, SetCommissionRequest request, CancellationToken cancellationToken)
        => partners.SetCommissionAsync(id, request.CommissionPercent, User.GetUserId(), cancellationToken);
}
