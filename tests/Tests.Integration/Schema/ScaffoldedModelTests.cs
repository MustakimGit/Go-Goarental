using GoGoaRentals.Entities.Data;
using Microsoft.EntityFrameworkCore;

namespace GoGoaRentals.Tests.Integration.Schema;

/// <summary>Verifies the scaffolded DbContext still matches the live schema (catches a forgotten re-scaffold).</summary>
public class ScaffoldedModelTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(TestDatabase.ConnectionString).Options);

    [Fact]
    public async Task Every_mapped_entity_can_be_queried()
    {
        await using var db = CreateContext();
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection(); // owned by the context

        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var sql = $"SELECT TOP (0) * FROM [{entityType.GetSchema() ?? "dbo"}].[{entityType.GetTableName()}]";
            var columns = entityType.GetProperties().Select(p => p.GetColumnName()).ToList();

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync();
            var schema = await reader.GetColumnSchemaAsync();
            var dbColumns = schema.Select(c => c.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = columns.Where(c => !dbColumns.Contains(c)).ToList();
            Assert.True(missing.Count == 0, $"{entityType.GetTableName()} is missing columns in DB: {string.Join(", ", missing)}");
        }
    }

    [Fact]
    public void Booking_has_one_to_many_assignments_and_calendar_blocks()
    {
        using var db = CreateContext();
        var booking = db.Model.FindEntityType(typeof(GoGoaRentals.Entities.Models.Booking))!;

        Assert.True(booking.FindNavigation("BookingAssignments")!.IsCollection);
        Assert.True(booking.FindNavigation("VehicleCalendarBlocks")!.IsCollection);
    }
}
