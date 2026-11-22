using GoGoaRentals.Entities.Data;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Common;

/// <summary>
/// Serializes calendar changes per vehicle with a SQL Server app lock owned by the current transaction,
/// so "check for overlaps, then insert a block" can't interleave between two requests.
/// </summary>
internal static class VehicleLock
{
    public static async Task AcquireAsync(AppDbContext db, int partnerVehicleId, CancellationToken cancellationToken)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("VehicleLock must be taken inside a transaction.");
        }

        var result = await db.Database.SqlQueryRaw<int>(
                """
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                SELECT @result AS [Value];
                """,
                $"vehicle:{partnerVehicleId}")
            .ToListAsync(cancellationToken);

        // 0 = granted, 1 = granted after waiting; negative = timeout, deadlock or error.
        if (result.Single() < 0)
        {
            throw new ConflictException("This vehicle is being updated by someone else. Please try again.");
        }
    }
}
