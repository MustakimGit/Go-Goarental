using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class VehicleCategory
{
    public int VehicleCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<VehicleType> VehicleTypes { get; set; } = new List<VehicleType>();
}
