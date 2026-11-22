using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class VehicleCalendarBlock
{
    public int CalendarBlockId { get; set; }

    public int PartnerVehicleId { get; set; }

    public DateTime StartAt { get; set; }

    public DateTime EndAt { get; set; }

    public byte BlockType { get; set; }

    public byte Status { get; set; }

    public int? SourceBookingId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual PartnerVehicle PartnerVehicle { get; set; } = null!;

    public virtual Booking? SourceBooking { get; set; }
}
