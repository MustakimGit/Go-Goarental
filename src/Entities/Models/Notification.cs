using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientUserId { get; set; }

    public int? BookingId { get; set; }

    public Guid? OutboxEventId { get; set; }

    public byte Channel { get; set; }

    public short EventType { get; set; }

    public int? TemplateId { get; set; }

    public string? RecipientAddress { get; set; }

    public string? Title { get; set; }

    public string? Body { get; set; }

    public byte NotificationStatus { get; set; }

    public bool IsRead { get; set; }

    public string? ProviderMessageId { get; set; }

    public string IdempotencyKey { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public string? FailureReason { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual ICollection<NotificationAttempt> NotificationAttempts { get; set; } = new List<NotificationAttempt>();

    public virtual User RecipientUser { get; set; } = null!;

    public virtual NotificationTemplate? Template { get; set; }
}
