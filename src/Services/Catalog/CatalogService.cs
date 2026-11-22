using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Catalog;

public sealed record VehicleCategoryDto(int VehicleCategoryId, string Name, bool IsActive, int VehicleTypeCount);

public sealed record VehicleTypeDto(
    int VehicleTypeId,
    int VehicleCategoryId,
    string CategoryName,
    string Name,
    string? Description,
    byte? SeatingCapacity,
    string? ImageUrl,
    VehicleTypeStatus Status);

public sealed record PickupLocationDto(
    int PickupLocationId,
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal CustomerDeliveryFee,
    bool IsActive);

/// <summary>One make + model from the shared catalog that partners choose from.</summary>
public sealed record VehicleModelDto(
    int VehicleId,
    int VehicleTypeId,
    string VehicleTypeName,
    string CategoryName,
    string Make,
    string Model,
    bool IsActive);

public sealed record SaveVehicleModelCommand(int VehicleTypeId, string Make, string Model, bool IsActive);

public sealed record SaveVehicleCategoryCommand(string Name, bool IsActive);

public sealed record SaveVehicleTypeCommand(
    int VehicleCategoryId,
    string Name,
    string? Description,
    byte? SeatingCapacity,
    string? ImageUrl,
    VehicleTypeStatus Status);

public sealed record SavePickupLocationCommand(
    string Name,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal CustomerDeliveryFee,
    bool IsActive);

/// <summary>Admin-managed lookups: vehicle categories, bookable vehicle types, the vehicle model catalog and pickup locations.</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<VehicleCategoryDto>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<VehicleCategoryDto> CreateCategoryAsync(SaveVehicleCategoryCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<VehicleCategoryDto> UpdateCategoryAsync(int id, SaveVehicleCategoryCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>With activeOnly, only active types in active categories.</summary>
    Task<IReadOnlyList<VehicleTypeDto>> ListVehicleTypesAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<VehicleTypeDto> CreateVehicleTypeAsync(SaveVehicleTypeCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<VehicleTypeDto> UpdateVehicleTypeAsync(int id, SaveVehicleTypeCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>With activeOnly, only active models whose type and category are active (what partners can pick).</summary>
    Task<IReadOnlyList<VehicleModelDto>> ListVehicleModelsAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<VehicleModelDto> CreateVehicleModelAsync(SaveVehicleModelCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Also updates the make, model and type copied onto partner vehicles of this model, so names stay identical.</summary>
    Task<VehicleModelDto> UpdateVehicleModelAsync(int id, SaveVehicleModelCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PickupLocationDto>> ListPickupLocationsAsync(bool activeOnly, CancellationToken cancellationToken = default);

    Task<PickupLocationDto> CreatePickupLocationAsync(SavePickupLocationCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<PickupLocationDto> UpdatePickupLocationAsync(int id, SavePickupLocationCommand command, int actorUserId, CancellationToken cancellationToken = default);
}

public sealed class CatalogService(AppDbContext db) : ICatalogService
{
    private static readonly byte ActiveTypeStatus = EnumValue.ToByte(VehicleTypeStatus.Active);

    // ---------------------------------------------------------------- Categories

    public async Task<IReadOnlyList<VehicleCategoryDto>> ListCategoriesAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => await db.VehicleCategories.AsNoTracking()
            .Where(c => !activeOnly || c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new VehicleCategoryDto(c.VehicleCategoryId, c.Name, c.IsActive, c.VehicleTypes.Count))
            .ToListAsync(cancellationToken);

    public async Task<VehicleCategoryDto> CreateCategoryAsync(SaveVehicleCategoryCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var category = new VehicleCategory();
        db.VehicleCategories.Add(category);
        Apply(category, command);
        await SaveAsync(actorUserId, "VehicleCategoryCreated", nameof(VehicleCategory), () => category.VehicleCategoryId, command, cancellationToken);
        return await GetCategoryAsync(category.VehicleCategoryId, cancellationToken);
    }

    public async Task<VehicleCategoryDto> UpdateCategoryAsync(int id, SaveVehicleCategoryCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var category = await db.VehicleCategories.SingleOrDefaultAsync(c => c.VehicleCategoryId == id, cancellationToken)
            ?? throw new NotFoundException("Vehicle category");
        Apply(category, command);
        await SaveAsync(actorUserId, "VehicleCategoryUpdated", nameof(VehicleCategory), () => id, command, cancellationToken);
        return await GetCategoryAsync(id, cancellationToken);
    }

    private static void Apply(VehicleCategory category, SaveVehicleCategoryCommand command)
    {
        category.Name = command.Name.Trim();
        category.IsActive = command.IsActive;
    }

    private async Task<VehicleCategoryDto> GetCategoryAsync(int id, CancellationToken cancellationToken)
        => await db.VehicleCategories.AsNoTracking()
            .Where(c => c.VehicleCategoryId == id)
            .Select(c => new VehicleCategoryDto(c.VehicleCategoryId, c.Name, c.IsActive, c.VehicleTypes.Count))
            .SingleAsync(cancellationToken);

    // ---------------------------------------------------------------- Vehicle types

    public async Task<IReadOnlyList<VehicleTypeDto>> ListVehicleTypesAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => await ProjectTypes(db.VehicleTypes.AsNoTracking()
                .Where(t => !activeOnly || (t.Status == ActiveTypeStatus && t.VehicleCategory.IsActive))
                .OrderBy(t => t.VehicleCategory.Name).ThenBy(t => t.Name))
            .ToListAsync(cancellationToken);

    public async Task<VehicleTypeDto> CreateVehicleTypeAsync(SaveVehicleTypeCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureCategoryExistsAsync(command.VehicleCategoryId, cancellationToken);
        var type = new VehicleType();
        db.VehicleTypes.Add(type);
        Apply(type, command);
        await SaveAsync(actorUserId, "VehicleTypeCreated", nameof(VehicleType), () => type.VehicleTypeId, command, cancellationToken);
        return await ProjectTypes(db.VehicleTypes.AsNoTracking().Where(t => t.VehicleTypeId == type.VehicleTypeId)).SingleAsync(cancellationToken);
    }

    public async Task<VehicleTypeDto> UpdateVehicleTypeAsync(int id, SaveVehicleTypeCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var type = await db.VehicleTypes.SingleOrDefaultAsync(t => t.VehicleTypeId == id, cancellationToken)
            ?? throw new NotFoundException("Vehicle type");
        await EnsureCategoryExistsAsync(command.VehicleCategoryId, cancellationToken);
        Apply(type, command);
        await SaveAsync(actorUserId, "VehicleTypeUpdated", nameof(VehicleType), () => id, command, cancellationToken);
        return await ProjectTypes(db.VehicleTypes.AsNoTracking().Where(t => t.VehicleTypeId == id)).SingleAsync(cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await db.VehicleCategories.AnyAsync(c => c.VehicleCategoryId == categoryId, cancellationToken))
        {
            throw new NotFoundException("Vehicle category");
        }
    }

    private static void Apply(VehicleType type, SaveVehicleTypeCommand command)
    {
        type.VehicleCategoryId = command.VehicleCategoryId;
        type.Name = command.Name.Trim();
        type.Description = NullIfBlank(command.Description);
        type.SeatingCapacity = command.SeatingCapacity;
        type.ImageUrl = NullIfBlank(command.ImageUrl);
        type.Status = EnumValue.ToByte(command.Status);
    }

    private static IQueryable<VehicleTypeDto> ProjectTypes(IQueryable<VehicleType> query)
        => query.Select(t => new VehicleTypeDto(
            t.VehicleTypeId,
            t.VehicleCategoryId,
            t.VehicleCategory.Name,
            t.Name,
            t.Description,
            t.SeatingCapacity,
            t.ImageUrl,
            // Status is validated by a CHECK constraint, so the cast can't produce an undefined value.
            (VehicleTypeStatus)t.Status));

    // ---------------------------------------------------------------- Vehicle models (make + model catalog)

    public async Task<IReadOnlyList<VehicleModelDto>> ListVehicleModelsAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => await ProjectModels(db.Vehicles.AsNoTracking()
                .Where(v => !activeOnly || (v.IsActive && v.VehicleType.Status == ActiveTypeStatus && v.VehicleType.VehicleCategory.IsActive))
                .OrderBy(v => v.VehicleType.Name).ThenBy(v => v.Make).ThenBy(v => v.Model))
            .ToListAsync(cancellationToken);

    public async Task<VehicleModelDto> CreateVehicleModelAsync(SaveVehicleModelCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureTypeExistsAsync(command.VehicleTypeId, cancellationToken);
        var vehicle = new Vehicle();
        db.Vehicles.Add(vehicle);
        Apply(vehicle, command);
        await SaveAsync(actorUserId, "VehicleModelCreated", nameof(Vehicle), () => vehicle.VehicleId, command, cancellationToken);
        return await ProjectModels(db.Vehicles.AsNoTracking().Where(v => v.VehicleId == vehicle.VehicleId)).SingleAsync(cancellationToken);
    }

    public async Task<VehicleModelDto> UpdateVehicleModelAsync(int id, SaveVehicleModelCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await db.Vehicles.Include(v => v.PartnerVehicles).SingleOrDefaultAsync(v => v.VehicleId == id, cancellationToken)
            ?? throw new NotFoundException("Vehicle model");
        await EnsureTypeExistsAsync(command.VehicleTypeId, cancellationToken);
        Apply(vehicle, command);

        // Partner vehicles keep copies of these fields; keep them identical to the catalog.
        var now = DateTime.UtcNow;
        foreach (var partnerVehicle in vehicle.PartnerVehicles)
        {
            partnerVehicle.Make = vehicle.Make;
            partnerVehicle.Model = vehicle.Model;
            partnerVehicle.VehicleTypeId = vehicle.VehicleTypeId;
            partnerVehicle.UpdatedAt = now;
        }

        await SaveAsync(actorUserId, "VehicleModelUpdated", nameof(Vehicle), () => id, command, cancellationToken);
        return await ProjectModels(db.Vehicles.AsNoTracking().Where(v => v.VehicleId == id)).SingleAsync(cancellationToken);
    }

    private async Task EnsureTypeExistsAsync(int typeId, CancellationToken cancellationToken)
    {
        if (!await db.VehicleTypes.AnyAsync(t => t.VehicleTypeId == typeId, cancellationToken))
        {
            throw new NotFoundException("Vehicle type");
        }
    }

    private static void Apply(Vehicle vehicle, SaveVehicleModelCommand command)
    {
        vehicle.VehicleTypeId = command.VehicleTypeId;
        vehicle.Make = command.Make.Trim();
        vehicle.Model = command.Model.Trim();
        vehicle.IsActive = command.IsActive;
    }

    private static IQueryable<VehicleModelDto> ProjectModels(IQueryable<Vehicle> query)
        => query.Select(v => new VehicleModelDto(
            v.VehicleId, v.VehicleTypeId, v.VehicleType.Name, v.VehicleType.VehicleCategory.Name, v.Make, v.Model, v.IsActive));

    // ---------------------------------------------------------------- Pickup locations

    public async Task<IReadOnlyList<PickupLocationDto>> ListPickupLocationsAsync(bool activeOnly, CancellationToken cancellationToken = default)
        => await ProjectLocations(db.PickupLocations.AsNoTracking().Where(l => !activeOnly || l.IsActive).OrderBy(l => l.Name))
            .ToListAsync(cancellationToken);

    public async Task<PickupLocationDto> CreatePickupLocationAsync(SavePickupLocationCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var location = new PickupLocation();
        db.PickupLocations.Add(location);
        Apply(location, command);
        await SaveAsync(actorUserId, "PickupLocationCreated", nameof(PickupLocation), () => location.PickupLocationId, command, cancellationToken);
        return await ProjectLocations(db.PickupLocations.AsNoTracking().Where(l => l.PickupLocationId == location.PickupLocationId)).SingleAsync(cancellationToken);
    }

    public async Task<PickupLocationDto> UpdatePickupLocationAsync(int id, SavePickupLocationCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var location = await db.PickupLocations.SingleOrDefaultAsync(l => l.PickupLocationId == id, cancellationToken)
            ?? throw new NotFoundException("Pickup location");
        Apply(location, command);
        await SaveAsync(actorUserId, "PickupLocationUpdated", nameof(PickupLocation), () => id, command, cancellationToken);
        return await ProjectLocations(db.PickupLocations.AsNoTracking().Where(l => l.PickupLocationId == id)).SingleAsync(cancellationToken);
    }

    private static void Apply(PickupLocation location, SavePickupLocationCommand command)
    {
        location.Name = command.Name.Trim();
        location.Address = command.Address.Trim();
        location.Latitude = command.Latitude;
        location.Longitude = command.Longitude;
        location.CustomerDeliveryFee = command.CustomerDeliveryFee;
        location.IsActive = command.IsActive;
    }

    private static IQueryable<PickupLocationDto> ProjectLocations(IQueryable<PickupLocation> query)
        => query.Select(l => new PickupLocationDto(
            l.PickupLocationId, l.Name, l.Address, l.Latitude, l.Longitude, l.CustomerDeliveryFee, l.IsActive));

    // ---------------------------------------------------------------- Shared

    /// <summary>Saves the entity, then writes the audit row (the id only exists after the first save).</summary>
    private async Task SaveAsync(int actorUserId, string action, string entityType, Func<int> id, object details, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsDuplicateKey(ex))
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Something with this name already exists.");
        }

        AuditWriter.Add(db, actorUserId, action, entityType, id(), details);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
