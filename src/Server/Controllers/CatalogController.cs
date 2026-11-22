using GoGoaRentals.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

/// <summary>Public, read-only catalog: only what customers and partners can currently choose.</summary>
[ApiController]
[Route("api/catalog")]
[AllowAnonymous]
public sealed class CatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("vehicle-types")]
    public Task<IReadOnlyList<VehicleTypeDto>> VehicleTypes(CancellationToken cancellationToken)
        => catalog.ListVehicleTypesAsync(activeOnly: true, cancellationToken);

    /// <summary>Makes and models partners choose from when adding a vehicle.</summary>
    [HttpGet("vehicle-models")]
    public Task<IReadOnlyList<VehicleModelDto>> VehicleModels(CancellationToken cancellationToken)
        => catalog.ListVehicleModelsAsync(activeOnly: true, cancellationToken);

    [HttpGet("pickup-locations")]
    public Task<IReadOnlyList<PickupLocationDto>> PickupLocations(CancellationToken cancellationToken)
        => catalog.ListPickupLocationsAsync(activeOnly: true, cancellationToken);
}
