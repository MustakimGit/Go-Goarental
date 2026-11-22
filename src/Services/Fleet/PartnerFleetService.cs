using System.Text.RegularExpressions;
using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using GoGoaRentals.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Fleet;

/// <summary>A rental partner managing their own vehicles. Every call is scoped to the caller's partner id.</summary>
public interface IPartnerFleetService
{
    Task<IReadOnlyList<PartnerVehicleDto>> ListAsync(int rentalPartnerId, CancellationToken cancellationToken = default);

    Task<PartnerVehicleDto> GetAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken = default);

    Task<PartnerVehicleDto> CreateAsync(int rentalPartnerId, SaveVehicleCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<PartnerVehicleDto> UpdateAsync(int rentalPartnerId, int vehicleId, SaveVehicleCommand command, int actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Partner switches a vehicle on (Active), pauses it (Inactive) or retires it for good.</summary>
    Task<PartnerVehicleDto> SetStatusAsync(int rentalPartnerId, int vehicleId, VehicleStatus target, int actorUserId, CancellationToken cancellationToken = default);

    Task<PartnerVehicleDto> UploadDocumentAsync(int rentalPartnerId, int vehicleId, UploadDocumentCommand command, int actorUserId, CancellationToken cancellationToken = default);

    Task<DocumentFile> OpenDocumentAsync(int rentalPartnerId, int documentId, CancellationToken cancellationToken = default);
}

public sealed partial class PartnerFleetService(AppDbContext db, IFileStorage storage, TimeProvider time) : IPartnerFleetService
{
    private const string DocumentFolder = "vehicle-documents";

    public async Task<IReadOnlyList<PartnerVehicleDto>> ListAsync(int rentalPartnerId, CancellationToken cancellationToken = default)
    {
        var vehicles = await FleetQueries.WithDetails(db)
            .Where(v => v.RentalPartnerId == rentalPartnerId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(cancellationToken);
        return vehicles.Select(FleetQueries.ToDto).ToList();
    }

    public async Task<PartnerVehicleDto> GetAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken = default)
    {
        var vehicle = await FleetQueries.WithDetails(db)
            .SingleOrDefaultAsync(v => v.PartnerVehicleId == vehicleId && v.RentalPartnerId == rentalPartnerId, cancellationToken)
            ?? throw new NotFoundException("Vehicle");
        return FleetQueries.ToDto(vehicle);
    }

    public async Task<PartnerVehicleDto> CreateAsync(int rentalPartnerId, SaveVehicleCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsurePartnerActiveAsync(rentalPartnerId, cancellationToken);
        var model = await ValidateReferencesAsync(command, cancellationToken);

        var now = DateTime.UtcNow;
        var vehicle = new PartnerVehicle
        {
            RentalPartnerId = rentalPartnerId,
            Status = EnumValue.ToByte(VehicleStatus.PendingVerification),
            DocumentVerificationStatus = EnumValue.ToByte(DocumentVerificationStatus.Pending),
            CreatedAt = now,
            UpdatedAt = now,
        };
        Apply(vehicle, command, model);
        db.PartnerVehicles.Add(vehicle);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SaveVehicleAsync(cancellationToken);
        AuditWriter.Add(db, actorUserId, "VehicleCreated", nameof(PartnerVehicle), vehicle.PartnerVehicleId,
            new { vehicle.RegistrationNumber, vehicle.VehicleTypeId });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(rentalPartnerId, vehicle.PartnerVehicleId, cancellationToken);
    }

    public async Task<PartnerVehicleDto> UpdateAsync(int rentalPartnerId, int vehicleId, SaveVehicleCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsurePartnerActiveAsync(rentalPartnerId, cancellationToken);
        var vehicle = await LoadOwnedAsync(rentalPartnerId, vehicleId, cancellationToken);
        EnsureNotRetired(vehicle);
        var model = await ValidateReferencesAsync(command, cancellationToken);

        // The documents were checked against this registration number; changing it would make them meaningless.
        var registration = NormalizeRegistration(command.RegistrationNumber);
        if (registration != vehicle.RegistrationNumber && vehicle.VehicleDocuments.Any())
        {
            throw new BusinessRuleException("The registration number can't be changed after documents are uploaded. Add the vehicle again instead.");
        }

        Apply(vehicle, command, model);
        vehicle.UpdatedAt = DateTime.UtcNow;
        AuditWriter.Add(db, actorUserId, "VehicleUpdated", nameof(PartnerVehicle), vehicleId, command);
        await SaveVehicleAsync(cancellationToken);

        return await GetAsync(rentalPartnerId, vehicleId, cancellationToken);
    }

    public async Task<PartnerVehicleDto> SetStatusAsync(int rentalPartnerId, int vehicleId, VehicleStatus target, int actorUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await LoadOwnedAsync(rentalPartnerId, vehicleId, cancellationToken);
        EnsureNotRetired(vehicle);
        var current = EnumValue.Parse<VehicleStatus>(vehicle.Status);

        switch (target)
        {
            case VehicleStatus.Active:
                await EnsurePartnerActiveAsync(rentalPartnerId, cancellationToken);
                // Back into service: live straight away if the paperwork is in order, otherwise wait for checks.
                vehicle.Status = EnumValue.ToByte(VehicleStatus.PendingVerification);
                VehicleVerification.Refresh(vehicle, GoaClock.Today(time));
                break;
            case VehicleStatus.Inactive:
                vehicle.Status = EnumValue.ToByte(VehicleStatus.Inactive);
                break;
            case VehicleStatus.Retired:
                // TODO(Phase 4): refuse while the vehicle has upcoming confirmed bookings.
                vehicle.Status = EnumValue.ToByte(VehicleStatus.Retired);
                break;
            default:
                throw new BusinessRuleException($"A vehicle can't be set to {target} directly.");
        }

        vehicle.UpdatedAt = DateTime.UtcNow;
        AuditWriter.Add(db, actorUserId, "VehicleStatusChanged", nameof(PartnerVehicle), vehicleId,
            new { From = current.ToString(), To = EnumValue.Parse<VehicleStatus>(vehicle.Status).ToString() });
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(rentalPartnerId, vehicleId, cancellationToken);
    }

    public async Task<PartnerVehicleDto> UploadDocumentAsync(int rentalPartnerId, int vehicleId, UploadDocumentCommand command, int actorUserId, CancellationToken cancellationToken = default)
    {
        var vehicle = await LoadOwnedAsync(rentalPartnerId, vehicleId, cancellationToken);
        EnsureNotRetired(vehicle);

        var today = GoaClock.Today(time);
        if (VehicleVerification.RequiresExpiryDate(command.DocumentType) && command.ExpiresOn is null)
        {
            throw new BusinessRuleException("Enter the date this document expires.");
        }

        if (command.ExpiresOn is { } expires && expires < today)
        {
            throw new BusinessRuleException("This document has already expired. Upload the current one.");
        }

        var (content, extension) = await DocumentFileRules.ReadAndValidateAsync(command.Content, cancellationToken);
        string key;
        await using (content)
        {
            key = await storage.SaveAsync(DocumentFolder, extension, content, cancellationToken);
        }

        try
        {
            var document = new VehicleDocument
            {
                DocumentType = EnumValue.ToByte(command.DocumentType),
                FileUrl = key,
                ExpiresOn = command.ExpiresOn,
                VerificationStatus = EnumValue.ToByte(DocumentVerificationStatus.Pending),
                CreatedAt = DateTime.UtcNow,
            };
            vehicle.VehicleDocuments.Add(document);
            // A new upload doesn't take a live vehicle offline: an older verified copy still counts until it expires.
            VehicleVerification.Refresh(vehicle, today);
            vehicle.UpdatedAt = DateTime.UtcNow;

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            AuditWriter.Add(db, actorUserId, "VehicleDocumentUploaded", nameof(VehicleDocument), document.VehicleDocumentId,
                new { vehicleId, Type = command.DocumentType.ToString(), command.ExpiresOn });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            storage.Delete(key);
            throw;
        }

        return await GetAsync(rentalPartnerId, vehicleId, cancellationToken);
    }

    public async Task<DocumentFile> OpenDocumentAsync(int rentalPartnerId, int documentId, CancellationToken cancellationToken = default)
    {
        var document = await db.VehicleDocuments.AsNoTracking()
            .Include(d => d.PartnerVehicle)
            .SingleOrDefaultAsync(d => d.VehicleDocumentId == documentId && d.PartnerVehicle.RentalPartnerId == rentalPartnerId, cancellationToken)
            ?? throw new NotFoundException("Document");
        return FleetQueries.OpenDocument(storage, document, document.PartnerVehicle.RegistrationNumber);
    }

    private async Task<PartnerVehicle> LoadOwnedAsync(int rentalPartnerId, int vehicleId, CancellationToken cancellationToken)
        => await db.PartnerVehicles
               .Include(v => v.VehicleDocuments)
               .SingleOrDefaultAsync(v => v.PartnerVehicleId == vehicleId && v.RentalPartnerId == rentalPartnerId, cancellationToken)
           ?? throw new NotFoundException("Vehicle");

    private async Task EnsurePartnerActiveAsync(int rentalPartnerId, CancellationToken cancellationToken)
    {
        var active = EnumValue.ToByte(PartnerStatus.Active);
        if (!await db.RentalPartners.AnyAsync(p => p.RentalPartnerId == rentalPartnerId && p.PartnerStatus == active, cancellationToken))
        {
            throw new BusinessRuleException("Your partner account must be approved before you can add or activate vehicles.");
        }
    }

    private static void EnsureNotRetired(PartnerVehicle vehicle)
    {
        if (vehicle.Status == EnumValue.ToByte(VehicleStatus.Retired))
        {
            throw new BusinessRuleException("This vehicle is retired and can't be changed.");
        }
    }

    /// <summary>Validates the command and returns the chosen catalog model (make, model and type come from it).</summary>
    private async Task<Vehicle> ValidateReferencesAsync(SaveVehicleCommand command, CancellationToken cancellationToken)
    {
        var activeType = EnumValue.ToByte(VehicleTypeStatus.Active);
        var model = await db.Vehicles.AsNoTracking()
            .SingleOrDefaultAsync(v => v.VehicleId == command.VehicleId && v.IsActive
                && v.VehicleType.Status == activeType && v.VehicleType.VehicleCategory.IsActive, cancellationToken)
            ?? throw new BusinessRuleException("Choose an available vehicle model.");

        if (!await db.PickupLocations.AnyAsync(l => l.PickupLocationId == command.PickupLocationId && l.IsActive, cancellationToken))
        {
            throw new BusinessRuleException("Choose an available pickup location.");
        }

        var thisYear = GoaClock.Today(time).Year;
        if (command.ManufacturingYear < 1990 || command.ManufacturingYear > thisYear + 1)
        {
            throw new BusinessRuleException($"Manufacturing year must be between 1990 and {thisYear + 1}.");
        }

        if (command.DeliveryRadiusKm is < 0 or > 100)
        {
            throw new BusinessRuleException("Delivery radius must be between 0 and 100 km.");
        }

        NormalizeRegistration(command.RegistrationNumber);
        return model;
    }

    private static void Apply(PartnerVehicle vehicle, SaveVehicleCommand command, Vehicle model)
    {
        // Copied from the catalog so every partner's vehicle of this model has identical names and type.
        vehicle.VehicleId = model.VehicleId;
        vehicle.VehicleTypeId = model.VehicleTypeId;
        vehicle.Make = model.Make;
        vehicle.Model = model.Model;
        vehicle.RegistrationNumber = NormalizeRegistration(command.RegistrationNumber);
        vehicle.ManufacturingYear = command.ManufacturingYear;
        vehicle.PickupLocationId = command.PickupLocationId;
        vehicle.DeliveryRadiusKm = command.DeliveryRadiusKm;
    }

    private async Task SaveVehicleAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsDuplicateKey(ex))
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("A vehicle with this registration number is already listed.");
        }
    }

    /// <summary>"GA 07 AB-1234" → "GA07AB1234". Accepts state-series and BH-series plates.</summary>
    public static string NormalizeRegistration(string value)
    {
        var normalized = NonAlphanumeric().Replace(value.ToUpperInvariant(), "");
        if (!StatePlate().IsMatch(normalized) && !BharatPlate().IsMatch(normalized))
        {
            throw new BusinessRuleException("Enter a valid registration number, e.g. GA07AB1234.");
        }

        return normalized;
    }

    [GeneratedRegex("[^A-Z0-9]")]
    private static partial Regex NonAlphanumeric();

    [GeneratedRegex(@"^[A-Z]{2}\d{1,2}[A-Z]{0,3}\d{1,4}$")]
    private static partial Regex StatePlate();

    [GeneratedRegex(@"^\d{2}BH\d{4}[A-Z]{1,2}$")]
    private static partial Regex BharatPlate();
}
