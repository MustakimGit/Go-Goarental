using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int BookingId { get; set; }

    public byte Gateway { get; set; }

    public string GatewayOrderId { get; set; } = null!;

    public string? GatewayPaymentId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = null!;

    public byte PaymentStatus { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual Booking Booking { get; set; } = null!;

    public virtual ICollection<Refund> Refunds { get; set; } = new List<Refund>();
}
