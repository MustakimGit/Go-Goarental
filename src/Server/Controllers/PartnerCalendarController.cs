using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Fleet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

[ApiController]
[Route("api/partner")]
[Authorize(Roles = Roles.Partner)]
public sealed class PartnerCalendarController(IVehicleCalendarService calendar) : ControllerBase
{
    [HttpGet("vehicles/{vehicleId:int}/calendar")]
    public Task<IReadOnlyList<CalendarBlockDto>> List(int vehicleId, CancellationToken cancellationToken)
        => calendar.ListUpcomingAsync(User.GetRentalPartnerId(), vehicleId, cancellationToken);

    [HttpPost("vehicles/{vehicleId:int}/calendar")]
    public Task<CalendarBlockDto> Add(int vehicleId, AddCalendarBlockRequest request, CancellationToken cancellationToken)
        => calendar.AddBlockAsync(
            User.GetRentalPartnerId(),
            vehicleId,
            new AddCalendarBlockCommand(request.BlockType!.Value, request.StartAt!.Value, request.EndAt!.Value, request.Notes),
            User.GetUserId(),
            cancellationToken);

    [HttpDelete("calendar/{blockId:int}")]
    public async Task<IActionResult> Remove(int blockId, CancellationToken cancellationToken)
    {
        await calendar.RemoveBlockAsync(User.GetRentalPartnerId(), blockId, User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
