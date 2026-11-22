using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class VehicleDocument
{
    public int VehicleDocumentId { get; set; }

    public int PartnerVehicleId { get; set; }

    public byte DocumentType { get; set; }

    public string FileUrl { get; set; } = null!;

    public DateOnly? ExpiresOn { get; set; }

    public byte VerificationStatus { get; set; }

    public int? VerifiedByUserId { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? ReviewNote { get; set; }

    public virtual PartnerVehicle PartnerVehicle { get; set; } = null!;

    public virtual User? VerifiedByUser { get; set; }
}
