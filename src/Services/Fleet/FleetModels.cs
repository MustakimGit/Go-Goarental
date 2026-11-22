using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Services.Fleet;

public sealed record VehicleDocumentDto(
    int VehicleDocumentId,
    VehicleDocumentType DocumentType,
    DateOnly? ExpiresOn,
    DocumentVerificationStatus VerificationStatus,
    string? ReviewNote,
    DateTime? VerifiedAt,
    DateTime CreatedAt);

public sealed record PartnerVehicleDto(
    int PartnerVehicleId,
    int RentalPartnerId,
    string BusinessName,
    int VehicleId,
    int VehicleTypeId,
    string VehicleTypeName,
    string RegistrationNumber,
    string Make,
    string Model,
    short ManufacturingYear,
    int PickupLocationId,
    string PickupLocationName,
    decimal DeliveryRadiusKm,
    VehicleStatus Status,
    DocumentVerificationStatus DocumentVerificationStatus,
    DateTime CreatedAt,
    IReadOnlyList<VehicleDocumentDto> Documents);

/// <summary>VehicleId picks the make + model from the shared catalog; the vehicle type comes from it too.</summary>
public sealed record SaveVehicleCommand(
    int VehicleId,
    string RegistrationNumber,
    short ManufacturingYear,
    int PickupLocationId,
    decimal DeliveryRadiusKm);

public sealed record UploadDocumentCommand(VehicleDocumentType DocumentType, DateOnly? ExpiresOn, Stream Content);

public sealed record DocumentFile(Stream Content, string ContentType, string DownloadName);
