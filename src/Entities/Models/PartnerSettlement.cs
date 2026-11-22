using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class PartnerSettlement
{
    public int SettlementId { get; set; }

    public int BookingId { get; set; }

    public int RentalPartnerId { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal CommissionAmount { get; set; }

    public decimal DeliveryCost { get; set; }

    public decimal OtherDeductions { get; set; }

    public decimal NetPayout { get; set; }

    public byte SettlementStatus { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public virtual Booking Booking { get; set; } = null!;

    public virtual RentalPartner RentalPartner { get; set; } = null!;
}
