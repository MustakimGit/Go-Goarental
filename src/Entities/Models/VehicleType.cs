using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class VehicleType
{
    public int VehicleTypeId { get; set; }

    public int VehicleCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public byte? SeatingCapacity { get; set; }

    public string? ImageUrl { get; set; }

    public byte Status { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<PartnerVehicle> PartnerVehicles { get; set; } = new List<PartnerVehicle>();

    public virtual VehicleCategory VehicleCategory { get; set; } = null!;

    public virtual ICollection<VehiclePricing> VehiclePricings { get; set; } = new List<VehiclePricing>();

    public virtual ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
