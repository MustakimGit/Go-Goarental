using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class NotificationOutbox
{
    public long OutboxId { get; set; }

    public Guid EventId { get; set; }

    public short EventType { get; set; }

    public string Payload { get; set; } = null!;

    public byte ProcessingStatus { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }
}
