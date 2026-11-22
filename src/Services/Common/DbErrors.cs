using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Services.Common;

internal static class DbErrors
{
    // Unique key / unique index violations.
    private static readonly int[] DuplicateKeyErrors = [2627, 2601];

    public static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is SqlException sql && DuplicateKeyErrors.Contains(sql.Number);
}
