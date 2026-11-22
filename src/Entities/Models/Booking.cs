using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class Booking
{
    public int BookingId { get; set; }

    public string BookingNumber { get; set; } = null!;

    public int CustomerId { get; set; }

    public int VehicleTypeId { get; set; }

    public int? PartnerVehicleId { get; set; }

    public int PickupLocationId { get; set; }

    public byte PickupMode { get; set; }

    public string? DeliveryAddress { get; set; }

    public DateTime PickupAt { get; set; }

    public DateTime ReturnAt { get; set; }

    public byte BookingStatus { get; set; }

    public decimal CustomerPrice { get; set; }

    public decimal DeliveryFee { get; set; }

    public decimal DepositAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = null!;

    public string PricingSnapshot { get; set; } = null!;

    public string IdempotencyKey { get; set; } = null!;

    public byte? CancellationReason { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual ICollection<BookingAssignment> BookingAssignments { get; set; } = new List<BookingAssignment>();

    public virtual User Customer { get; set; } = null!;

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PartnerSettlement? PartnerSettlement { get; set; }

    public virtual PartnerVehicle? PartnerVehicle { get; set; }

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual PickupLocation PickupLocation { get; set; } = null!;

    public virtual ICollection<VehicleCalendarBlock> VehicleCalendarBlocks { get; set; } = new List<VehicleCalendarBlock>();

    public virtual VehicleType VehicleType { get; set; } = null!;
}
