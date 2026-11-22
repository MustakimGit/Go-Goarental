using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminVehiclesController(IVehicleReviewService reviews) : ControllerBase
{
    /// <summary>All partner vehicles, or with needsReview=true only those with documents awaiting a decision.</summary>
    [HttpGet("vehicles")]
    public Task<IReadOnlyList<PartnerVehicleDto>> List([FromQuery] bool needsReview, CancellationToken cancellationToken)
        => reviews.ListAsync(needsReview, cancellationToken);

    [HttpPut("vehicle-documents/{documentId:int}/review")]
    public Task<PartnerVehicleDto> Review(int documentId, ReviewDocumentRequest request, CancellationToken cancellationToken)
        => reviews.ReviewDocumentAsync(documentId, request.Approve!.Value, request.Reason, User.GetUserId(), cancellationToken);

    [HttpGet("vehicle-documents/{documentId:int}/file")]
    public async Task<IActionResult> DownloadDocument(int documentId, CancellationToken cancellationToken)
    {
        var file = await reviews.OpenDocumentAsync(documentId, cancellationToken);
        return DocumentFileResult.Inline(this, file);
    }
}
