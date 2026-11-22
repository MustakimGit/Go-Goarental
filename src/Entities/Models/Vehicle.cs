using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class Vehicle
{
    public int VehicleId { get; set; }

    public int VehicleTypeId { get; set; }

    public string Make { get; set; } = null!;

    public string Model { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<PartnerVehicle> PartnerVehicles { get; set; } = new List<PartnerVehicle>();

    public virtual VehicleType VehicleType { get; set; } = null!;
}
