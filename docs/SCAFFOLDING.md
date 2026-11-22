# Database-first workflow (SSMS → EF Core)

SQL Server is the source of truth. You change the schema in SSMS, save the change as a numbered script,
then regenerate the EF Core classes. EF Core migrations are **not** used.

## Where things live

| Path | What | Edit by hand? |
|---|---|---|
| `database/NNN_*.sql` | Schema scripts, applied in order | Add new files only; never edit one that's been applied |
| `src/Entities/Data/AppDbContext.cs` | Generated DbContext | **No** — overwritten on every scaffold |
| `src/Entities/Models/*.cs` | Generated entity classes | **No** — overwritten on every scaffold |
| `src/Entities/Data/AppDbContext.Partial.cs` | Extra model config via `OnModelCreatingPartial` | Yes |
| `src/Entities/Models/Partials/*.cs` | `partial class` additions (typed enum accessors, helpers) | Yes |
| `src/Entities/Enums/*.cs` | C# enums for TINYINT status columns | Yes |

Partial files must use the same namespace as the generated class (`GoGoaRentals.Entities.Models`) and
must not live under a name the scaffolder writes to (it only writes `Models/<Table>.cs` and `Data/AppDbContext.cs`).

## Scaffold command

Run from the repository root (`GoGoaRentals/`). The connection string is read from
`src/Server/appsettings.Development.json` by name, so it never appears on the command line.

```powershell
dotnet tool restore
dotnet ef dbcontext scaffold "Name=ConnectionStrings:DefaultConnection" Microsoft.EntityFrameworkCore.SqlServer `
  --project src/Entities --startup-project src/Server `
  --context AppDbContext --context-dir Data --context-namespace GoGoaRentals.Entities.Data `
  --output-dir Models --namespace GoGoaRentals.Entities.Models `
  --no-onconfiguring --force
```

- `--force` overwrites only the generated files listed above. Your partial classes and enums are untouched.
- `--no-onconfiguring` keeps the connection string out of generated code; `Program.cs` supplies it.
- Do not add `--data-annotations` or `--use-database-names` later without regenerating everything and reviewing the diff — it changes every file.
- Tool and provider versions are pinned to EF Core **8.0.33** (`.config/dotnet-tools.json`, `Entities.csproj`). Keep them equal.

## Making a schema change

1. Write the change as `database/NNN_short_description.sql` (next number). Wrap it in a transaction and start with
   `SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON;` (needed for filtered indexes when run via sqlcmd).
2. Run it in SSMS against `(localdb)\MSSQLLocalDB` → `GoGoaRentals`.
3. Run the scaffold command.
4. `git diff src/Entities` — review what changed.
5. Find dependents of anything renamed or removed: services, WebModels (DTOs), LINQ queries, tests.
6. `dotnet build` and `dotnet test`. The integration tests fail if the code and schema have drifted.

### New table / column / relationship
Nothing special: steps 1–6. New navigation properties appear automatically from foreign keys.

**Gotcha:** EF treats any *unique* index whose columns exactly equal a foreign key (even a filtered one) as a
**one-to-one** relationship. If you need "at most one active X per parent", include a second column that is
constant within the filter — see `database/002_fix_one_to_many_unique_indexes.sql`.

## Enums

Stable lifecycle states are stored as `TINYINT` (or `SMALLINT` for `NotificationEventType`) with a
`CHECK (Col IN (...))` constraint. Each maps to a C# enum in `src/Entities/Enums` with **explicit values**.
Admin-managed lists (vehicle categories, pickup locations, pricing) are tables, not enums.

Scaffolding generates `byte` properties. Convert with `EnumValue.Parse<TEnum>(byte)` /
`EnumValue.ToByte(value)`, which throw `InvalidEnumValueException` for unknown values, instead of bare casts.
Optionally expose a typed accessor in a partial class:

```csharp
// src/Entities/Models/Partials/Booking.cs
namespace GoGoaRentals.Entities.Models;

public partial class Booking
{
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public Enums.BookingStatus Status
    {
        get => Enums.EnumValue.Parse<Enums.BookingStatus>(BookingStatus);
        set => BookingStatus = Enums.EnumValue.ToByte(value);
    }
}
```

Query with the byte value (`b.BookingStatus == (byte)BookingStatus.Confirmed`), since `[NotMapped]` properties
can't be translated to SQL.

### Adding an enum value
1. Add the member with the next unused number. **Never renumber or reuse a value** that has been deployed.
2. New script: drop and recreate the CHECK constraint with the new value.
3. Scaffold (no entity change expected — the column is still `byte`).
4. `dotnet test` — `EnumCheckConstraintTests` confirms the enum and the CHECK constraint agree.
5. Add the user-facing label in the frontend label map.

Labels are never stored in the database; the UI maps values to display text.

## Fresh database from scratch

```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE GoGoaRentals"
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
  sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d GoGoaRentals -i $_.FullName
}
```
