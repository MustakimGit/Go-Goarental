using Microsoft.Data.SqlClient;

namespace GoGoaRentals.Tests.Integration;

/// <summary>
/// The suite runs against its own database, GoGoaRentals_Tests on LocalDB, rebuilt from database/*.sql once per
/// test run so the development database stays clean. Set GOGOA_TEST_CONNECTION to use an existing database instead
/// (it must already have the schema; it is never dropped).
/// </summary>
internal static class TestDatabase
{
    private const string DatabaseName = "GoGoaRentals_Tests";

    private static readonly Lazy<string> Connection = new(Initialize, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string ConnectionString => Connection.Value;

    private static string Initialize()
    {
        var configured = Environment.GetEnvironmentVariable("GOGOA_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = @"(localdb)\MSSQLLocalDB",
            IntegratedSecurity = true,
            TrustServerCertificate = true,
            InitialCatalog = "master",
        };

        using (var master = new SqlConnection(builder.ConnectionString))
        {
            master.Open();
            Execute(master, $"""
                IF DB_ID('{DatabaseName}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{DatabaseName}];
                END;
                CREATE DATABASE [{DatabaseName}];
                """);
        }

        SqlConnection.ClearAllPools();
        builder.InitialCatalog = DatabaseName;

        using (var db = new SqlConnection(builder.ConnectionString))
        {
            db.Open();
            foreach (var script in Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "database"), "*.sql").Order(StringComparer.Ordinal))
            {
                Execute(db, File.ReadAllText(script));
            }
        }

        return builder.ConnectionString;
    }

    private static void Execute(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        command.ExecuteNonQuery();
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GoGoaRentals.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not find the repository root (GoGoaRentals.sln) above " + AppContext.BaseDirectory);
    }
}
