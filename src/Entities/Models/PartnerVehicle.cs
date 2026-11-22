using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class PartnerVehicle
{
    public int PartnerVehicleId { get; set; }

    public int RentalPartnerId { get; set; }

    public int VehicleTypeId { get; set; }

    public string RegistrationNumber { get; set; } = null!;

    public string Make { get; set; } = null!;

    public string Model { get; set; } = null!;

    public short ManufacturingYear { get; set; }

    public byte Status { get; set; }

    public int PickupLocationId { get; set; }

    public decimal DeliveryRadiusKm { get; set; }

    public byte DocumentVerificationStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int VehicleId { get; set; }

    public virtual ICollection<BookingAssignment> BookingAssignments { get; set; } = new List<BookingAssignment>();

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual PickupLocation PickupLocation { get; set; } = null!;

    public virtual RentalPartner RentalPartner { get; set; } = null!;

    public virtual Vehicle Vehicle { get; set; } = null!;

    public virtual ICollection<VehicleCalendarBlock> VehicleCalendarBlocks { get; set; } = new List<VehicleCalendarBlock>();

    public virtual ICollection<VehicleDocument> VehicleDocuments { get; set; } = new List<VehicleDocument>();

    public virtual VehicleType VehicleType { get; set; } = null!;
}
