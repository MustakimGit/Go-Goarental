using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class VehiclePricing
{
    public int PricingId { get; set; }

    public int VehicleTypeId { get; set; }

    public byte DurationType { get; set; }

    public decimal CustomerPrice { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal TaxPercent { get; set; }

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual VehicleType VehicleType { get; set; } = null!;
}
