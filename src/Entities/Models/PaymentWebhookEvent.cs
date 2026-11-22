using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class PaymentWebhookEvent
{
    public long WebhookEventId { get; set; }

    public byte Gateway { get; set; }

    public string GatewayEventId { get; set; } = null!;

    public string EventType { get; set; } = null!;

    public string Payload { get; set; } = null!;

    public byte ProcessingStatus { get; set; }

    public DateTime ReceivedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? FailureReason { get; set; }
}
