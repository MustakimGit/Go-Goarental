using System.Text.Json;
using GoGoaRentals.Entities.Data;
using GoGoaRentals.Entities.Models;

namespace GoGoaRentals.Services.Common;

internal static class AuditWriter
{
    /// <summary>Adds an audit row to the context; it is saved with the caller's SaveChanges. Never pass secrets in details.</summary>
    public static void Add(AppDbContext db, int? actorUserId, string action, string entityType, object entityId, object? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString()!,
            Timestamp = DateTime.UtcNow,
            Details = details is null ? null : JsonSerializer.Serialize(details),
        });
    }
}
