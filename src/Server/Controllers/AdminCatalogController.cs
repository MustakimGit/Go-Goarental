using GoGoaRentals.Server.Infrastructure;
using GoGoaRentals.Server.WebModels;
using GoGoaRentals.Services.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoGoaRentals.Server.Controllers;

/// <summary>Admin management of vehicle categories, vehicle types and pickup locations (including inactive ones).</summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminCatalogController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("vehicle-categories")]
    public Task<IReadOnlyList<VehicleCategoryDto>> ListCategories(CancellationToken cancellationToken)
        => catalog.ListCategoriesAsync(activeOnly: false, cancellationToken);

    [HttpPost("vehicle-categories")]
    public Task<VehicleCategoryDto> CreateCategory(SaveVehicleCategoryRequest request, CancellationToken cancellationToken)
        => catalog.CreateCategoryAsync(ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("vehicle-categories/{id:int}")]
    public Task<VehicleCategoryDto> UpdateCategory(int id, SaveVehicleCategoryRequest request, CancellationToken cancellationToken)
        => catalog.UpdateCategoryAsync(id, ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpGet("vehicle-types")]
    public Task<IReadOnlyList<VehicleTypeDto>> ListVehicleTypes(CancellationToken cancellationToken)
        => catalog.ListVehicleTypesAsync(activeOnly: false, cancellationToken);

    [HttpPost("vehicle-types")]
    public Task<VehicleTypeDto> CreateVehicleType(SaveVehicleTypeRequest request, CancellationToken cancellationToken)
        => catalog.CreateVehicleTypeAsync(ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("vehicle-types/{id:int}")]
    public Task<VehicleTypeDto> UpdateVehicleType(int id, SaveVehicleTypeRequest request, CancellationToken cancellationToken)
        => catalog.UpdateVehicleTypeAsync(id, ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpGet("vehicle-models")]
    public Task<IReadOnlyList<VehicleModelDto>> ListVehicleModels(CancellationToken cancellationToken)
        => catalog.ListVehicleModelsAsync(activeOnly: false, cancellationToken);

    [HttpPost("vehicle-models")]
    public Task<VehicleModelDto> CreateVehicleModel(SaveVehicleModelRequest request, CancellationToken cancellationToken)
        => catalog.CreateVehicleModelAsync(ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("vehicle-models/{id:int}")]
    public Task<VehicleModelDto> UpdateVehicleModel(int id, SaveVehicleModelRequest request, CancellationToken cancellationToken)
        => catalog.UpdateVehicleModelAsync(id, ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpGet("pickup-locations")]
    public Task<IReadOnlyList<PickupLocationDto>> ListPickupLocations(CancellationToken cancellationToken)
        => catalog.ListPickupLocationsAsync(activeOnly: false, cancellationToken);

    [HttpPost("pickup-locations")]
    public Task<PickupLocationDto> CreatePickupLocation(SavePickupLocationRequest request, CancellationToken cancellationToken)
        => catalog.CreatePickupLocationAsync(ToCommand(request), User.GetUserId(), cancellationToken);

    [HttpPut("pickup-locations/{id:int}")]
    public Task<PickupLocationDto> UpdatePickupLocation(int id, SavePickupLocationRequest request, CancellationToken cancellationToken)
        => catalog.UpdatePickupLocationAsync(id, ToCommand(request), User.GetUserId(), cancellationToken);

    private static SaveVehicleCategoryCommand ToCommand(SaveVehicleCategoryRequest r) => new(r.Name, r.IsActive);

    private static SaveVehicleTypeCommand ToCommand(SaveVehicleTypeRequest r)
        => new(r.VehicleCategoryId, r.Name, r.Description, r.SeatingCapacity, r.ImageUrl, r.Status);

    private static SaveVehicleModelCommand ToCommand(SaveVehicleModelRequest r) => new(r.VehicleTypeId, r.Make, r.Model, r.IsActive);

    private static SavePickupLocationCommand ToCommand(SavePickupLocationRequest r)
        => new(r.Name, r.Address, r.Latitude, r.Longitude, r.CustomerDeliveryFee, r.IsActive);
}
