using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class PartnerServiceArea
{
    public int PartnerServiceAreaId { get; set; }

    public int RentalPartnerId { get; set; }

    public int PickupLocationId { get; set; }

    public bool DeliveryAvailable { get; set; }

    public decimal DeliveryCost { get; set; }

    public bool IsActive { get; set; }

    public virtual PickupLocation PickupLocation { get; set; } = null!;

    public virtual RentalPartner RentalPartner { get; set; } = null!;
}
