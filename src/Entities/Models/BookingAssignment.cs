using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class BookingAssignment
{
    public int AssignmentId { get; set; }

    public int BookingId { get; set; }

    public int PartnerVehicleId { get; set; }

    public int RentalPartnerId { get; set; }

    public short AttemptNumber { get; set; }

    public byte AssignmentStatus { get; set; }

    public decimal? RankScore { get; set; }

    public DateTime? AssignedAt { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual PartnerVehicle PartnerVehicle { get; set; } = null!;

    public virtual RentalPartner RentalPartner { get; set; } = null!;
}
