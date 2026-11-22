using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

/// <summary>A rental partner's own vehicles. The partner id always comes from the auth cookie, never the request.</summary>
[ApiController]
[Route("api/partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class PartnerVehiclesController(IPartnerFleetService fleet) : ControllerBase
{
    private const long MaxUploadBytes = 6 * 1024 * 1024; // 5 MB file plus form overhead

    [HttpGet("vehicles")]
    public Task<IReadOnlyList<PartnerVehicleDto>> List(CancellationToken cancellationToken)
        => fleet.ListAsync(User.GetRentalPartnerId(), cancellationToken);

    [HttpGet("vehicles/{id:int}")]
    public Task<PartnerVehicleDto> Get(int id, CancellationToken cancellationToken)
        => fleet.GetAsync(User.GetRentalPartnerId(), id, cancellationToken);

    [HttpPost("vehicles")]
    public Task<PartnerVehicleDto> Create(SaveVehicleRequest request, CancellationToken cancellationToken)
        => fleet.CreateAsync(User.GetRentalPartnerId(), ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("vehicles/{id:int}")]
    public Task<PartnerVehicleDto> Update(int id, SaveVehicleRequest request, CancellationToken cancellationToken)
        => fleet.UpdateAsync(User.GetRentalPartnerId(), id, ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("vehicles/{id:int}/status")]
    public Task<PartnerVehicleDto> SetStatus(int id, SetVehicleStatusRequest request, CancellationToken cancellationToken)
        => fleet.SetStatusAsync(User.GetRentalPartnerId(), id, request.Status!.Value, User.GetUserId(), cancellationToken);

    [HttpPost("vehicles/{id:int}/documents")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<PartnerVehicleDto> UploadDocument(int id, [FromForm] UploadDocumentRequest request, CancellationToken cancellationToken)
    {
        await using var content = request.File!.OpenReadStream();
        return await fleet.UploadDocumentAsync(
            User.GetRentalPartnerId(),
            id,
            new UploadDocumentCommand(request.DocumentType!.Value, request.ExpiresOn, content),
            User.GetUserId(),
            cancellationToken);
    }

    [HttpGet("documents/{documentId:int}/file")]
    public async Task<IActionResult> DownloadDocument(int documentId, CancellationToken cancellationToken)
    {
        var file = await fleet.OpenDocumentAsync(User.GetRentalPartnerId(), documentId, cancellationToken);
        return DocumentFileResult.Inline(this, file);
    }

    private static SaveVehicleCommand ToCommand(SaveVehicleRequest r) => new(
        r.VehicleId, r.RegistrationNumber, r.ManufacturingYear, r.PickupLocationId, r.DeliveryRadiusKm);
}
