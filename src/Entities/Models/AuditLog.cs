using System;
using System.Collections.Generic;

namespace GoGoaRentals.Entities.Models;

public partial class AuditLog
{
    public long AuditLogId { get; set; }

    public int? ActorUserId { get; set; }

    public string Action { get; set; } = null!;

    public string EntityType { get; set; } = null!;

    public string EntityId { get; set; } = null!;

    public DateTime Timestamp { get; set; }

    public string? CorrelationId { get; set; }

    public string? Details { get; set; }

    public virtual User? ActorUser { get; set; }
}
