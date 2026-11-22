using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class PickupLocation
{
    public int PickupLocationId { get; set; }

    public string Name { get; set; } = null!;

    public string Address { get; set; } = null!;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public decimal CustomerDeliveryFee { get; set; }

    public bool IsActive { get; set; }

    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();

    public virtual ICollection<PartnerServiceArea> PartnerServiceAreas { get; set; } = new List<PartnerServiceArea>();

    public virtual ICollection<PartnerVehicle> PartnerVehicles { get; set; } = new List<PartnerVehicle>();
}
