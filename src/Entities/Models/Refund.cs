using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class Refund
{
    public int RefundId { get; set; }

    public int PaymentId { get; set; }

    public string? GatewayRefundId { get; set; }

    public decimal Amount { get; set; }

    public byte RefundStatus { get; set; }

    public string? Reason { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public int? RequestedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Payment Payment { get; set; } = null!;

    public virtual User? RequestedByUser { get; set; }
}
