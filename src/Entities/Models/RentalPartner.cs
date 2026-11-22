using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class RentalPartner
{
    public int RentalPartnerId { get; set; }

    public int UserId { get; set; }

    public string BusinessName { get; set; } = null!;

    public string ContactPhone { get; set; } = null!;

    public string? ContactEmail { get; set; }

    public string? Address { get; set; }

    public byte PartnerStatus { get; set; }

    public decimal ReliabilityScore { get; set; }

    public decimal CommissionPercent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<BookingAssignment> BookingAssignments { get; set; } = new List<BookingAssignment>();

    public virtual ICollection<PartnerServiceArea> PartnerServiceAreas { get; set; } = new List<PartnerServiceArea>();

    public virtual ICollection<PartnerSettlement> PartnerSettlements { get; set; } = new List<PartnerSettlement>();

    public virtual ICollection<PartnerVehicle> PartnerVehicles { get; set; } = new List<PartnerVehicle>();

    public virtual User User { get; set; } = null!;
}
