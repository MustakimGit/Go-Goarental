using System.ComponentModel.DataAnnotations;
using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Server.WebModels;

public sealed class SaveVehicleRequest
{
    /// <summary>The make + model chosen from the shared catalog (GET /api/catalog/vehicle-models).</summary>
    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    [Required, MaxLength(20)]
    public string RegistrationNumber { get; set; } = "";

    [Range(1990, 2100)]
    public short ManufacturingYear { get; set; }

    [Range(1, int.MaxValue)]
    public int PickupLocationId { get; set; }

    [Range(0, 100)]
    public decimal DeliveryRadiusKm { get; set; }
}

public sealed class SetVehicleStatusRequest
{
    [Required]
    public VehicleStatus? Status { get; set; }
}

public sealed class UploadDocumentRequest
{
    [Required]
    public VehicleDocumentType? DocumentType { get; set; }

    public DateOnly? ExpiresOn { get; set; }

    [Required]
    public IFormFile? File { get; set; }
}
