using System.ComponentModel.DataAnnotations;
using GoGoaRentals.Entities.Enums;

namespace GoGoaRentals.Server.WebModels;

public sealed class ChangePartnerStatusRequest
{
    [Required]
    public PartnerStatus? Status { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}

public sealed class SetCommissionRequest
{
    [Range(0, 100)]
    public decimal CommissionPercent { get; set; }
}

public sealed class SaveVehicleCategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class SaveVehicleTypeRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleCategoryId { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(1, 60)]
    public byte? SeatingCapacity { get; set; }

    [Url, MaxLength(500)]
    public string? ImageUrl { get; set; }

    public VehicleTypeStatus Status { get; set; } = VehicleTypeStatus.Active;
}

public sealed class SaveVehicleModelRequest
{
    [Range(1, int.MaxValue)]
    public int VehicleTypeId { get; set; }

    [Required, MaxLength(100)]
    public string Make { get; set; } = "";

    [Required, MaxLength(100)]
    public string Model { get; set; } = "";

    public bool IsActive { get; set; } = true;
}

public sealed class SavePickupLocationRequest
{
    [Required, MaxLength(150)]
    public string Name { get; set; } = "";

    [Required, MaxLength(500)]
    public string Address { get; set; } = "";

    [Range(-90, 90)]
    public decimal Latitude { get; set; }

    [Range(-180, 180)]
    public decimal Longitude { get; set; }

    [Range(0, 100000)]
    public decimal CustomerDeliveryFee { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class ReviewDocumentRequest
{
    [Required]
    public bool? Approve { get; set; }

    [MaxLength(500)]
    public string? Reason { get; set; }
}
