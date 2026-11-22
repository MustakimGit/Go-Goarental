using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Enums;
using GoGoaRentals.Entities.Models;
using GoGoaRentals.Services.Common;
using GoGoaRentals.Services.Storage;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Fleet;

/// <summary>Read helpers shared by the partner and admin fleet services.</summary>
internal static class FleetQueries
{
    public static IQueryable<PartnerVehicle> WithDetails(AppDbContext db)
        => db.PartnerVehicles.AsNoTracking()
            .Include(v => v.RentalPartner)
            .Include(v => v.VehicleType)
            .Include(v => v.PickupLocation)
            .Include(v => v.VehicleDocuments);

    public static PartnerVehicleDto ToDto(PartnerVehicle v) => new(
        v.PartnerVehicleId,
        v.RentalPartnerId,
        v.RentalPartner.BusinessName,
        v.VehicleId,
        v.VehicleTypeId,
        v.VehicleType.Name,
        v.RegistrationNumber,
        v.Make,
        v.Model,
        v.ManufacturingYear,
        v.PickupLocationId,
        v.PickupLocation.Name,
        v.DeliveryRadiusKm,
        EnumValue.Parse<VehicleStatus>(v.Status),
        EnumValue.Parse<DocumentVerificationStatus>(v.DocumentVerificationStatus),
        v.CreatedAt,
        v.VehicleDocuments
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.VehicleDocumentId)
            .Select(d => new VehicleDocumentDto(
                d.VehicleDocumentId,
                EnumValue.Parse<VehicleDocumentType>(d.DocumentType),
                d.ExpiresOn,
                EnumValue.Parse<DocumentVerificationStatus>(d.VerificationStatus),
                d.ReviewNote,
                d.VerifiedAt,
                d.CreatedAt))
            .ToList());

    public static DocumentFile OpenDocument(IFileStorage storage, VehicleDocument document, string registrationNumber)
    {
        var stream = storage.OpenRead(document.FileUrl) ?? throw new NotFoundException("Document file");
        var type = EnumValue.Parse<VehicleDocumentType>(document.DocumentType);
        var extension = Path.GetExtension(document.FileUrl);
        return new DocumentFile(stream, DocumentFileRules.ContentTypeFor(document.FileUrl), $"{registrationNumber}-{type}{extension}");
    }
}
