using GoGoaRentals.Entities.Enums;
using Microsoft.Data.SqlClient;

namespace GoGoaRentals.Tests.Integration.Schema;

/// <summary>The Role table must hold exactly one row per UserRole member, with the same id and name (the auth role claim).</summary>
public class RoleTableTests
{
    [Fact]
    public async Task Role_rows_match_UserRole_enum()
    {
        await using var connection = new SqlConnection(TestDatabase.ConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand("SELECT RoleId, Name FROM dbo.Role ORDER BY RoleId", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var inDb = new List<(int Id, string Name)>();
        while (await reader.ReadAsync())
        {
            inDb.Add((reader.GetByte(0), reader.GetString(1)));
        }

        var inCode = Enum.GetValues<UserRole>().Select(r => ((int)r, r.ToString())).Order().ToList();

        Assert.Equal(inCode, inDb);
    }
}
