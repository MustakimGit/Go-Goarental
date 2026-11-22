using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class NotificationAttempt
{
    public long NotificationAttemptId { get; set; }

    public long NotificationId { get; set; }

    public short AttemptNumber { get; set; }

    public DateTime AttemptedAt { get; set; }

    public byte Result { get; set; }

    public string? ProviderResponse { get; set; }

    public string? ErrorMessage { get; set; }

    public virtual Notification Notification { get; set; } = null!;
}
