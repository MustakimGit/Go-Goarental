using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

/// <summary>Public search and quotes. Prices always come from the server; the client never sends an amount.</summary>
[ApiController]
[Route("api")]
[AllowAnonymous]
public sealed class SearchController(IQuoteService quotes) : ControllerBase
{
    [HttpGet("search")]
    public Task<IReadOnlyList<Quote>> Search([FromQuery] SearchQuery query, CancellationToken cancellationToken)
        => quotes.SearchAsync(new SearchRequest(query.PickupLocationId, query.PickupMode, query.PickupAt!.Value, query.ReturnAt!.Value), cancellationToken);

    [HttpPost("quotes")]
    public Task<Quote> Quote(QuoteRequestModel request, CancellationToken cancellationToken)
        => quotes.QuoteAsync(
            new QuoteRequest(request.VehicleTypeId, request.PickupLocationId, request.PickupMode, request.PickupAt!.Value, request.ReturnAt!.Value),
            cancellationToken);
}
