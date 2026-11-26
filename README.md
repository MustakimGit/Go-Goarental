# GoGoaRentals

Fixed-price two-wheeler and four-wheeler rental marketplace for Goa. See [docs/PLAN.md](docs/PLAN.md) for the
architecture and roadmap and [docs/SCAFFOLDING.md](docs/SCAFFOLDING.md) for the database-first workflow.

| Project | Path | Purpose |
|---|---|---|
| Client | `src/Client` | React + Vite + Tailwind + shadcn/ui (JavaScript/JSX) |
| Server | `src/Server` | ASP.NET Core 8 Web API — controllers, DTOs, middleware, background jobs |
| Services | `src/Services` | Business logic, allocation, payments, notifications |
| Entities | `src/Entities` | Scaffolded EF Core DbContext + entities, enums |
| Tests.Unit / Tests.Integration | `tests/` | xUnit; integration tests use real SQL Server |

## Prerequisites

- .NET SDK 8.0
- Node.js 22.12+ (npm included)
- SQL Server LocalDB (`(localdb)\MSSQLLocalDB`) — ships with Visual Studio / SQL Server Express
- SSMS (optional, for schema work) — connect to `(localdb)\MSSQLLocalDB` with Windows Authentication

## First-time setup

```powershell
# from the repository root
dotnet tool restore

# database
sqllocaldb start MSSQLLocalDB
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE GoGoaRentals"
Get-ChildItem database\*.sql | Sort-Object Name | ForEach-Object {
  sqlcmd -S "(localdb)\MSSQLLocalDB" -E -b -d GoGoaRentals -i $_.FullName
}

# frontend packages
cd src/Client; npm install; cd ../..
```

The local connection string is in `src/Server/appsettings.Development.json` (Windows authentication, no password).
For other environments set `ConnectionStrings__DefaultConnection` as an environment variable.

## Run

```powershell
dotnet run --project src/Server --launch-profile http   # API on http://localhost:5080 (Swagger at /swagger)
cd src/Client; npm run dev                               # UI on http://localhost:5173, proxies /api and /hubs
```

Health check: <http://localhost:5173/api/health> → `Healthy`.

## Test

```powershell
dotnet test GoGoaRentals.sln
cd src/Client; npm run build; npm run lint
```

Integration tests create their own LocalDB database, `GoGoaRentals_Tests`, from `database/*.sql` on every run, so
your development data is never touched. To use an existing database instead (never dropped), set
`GOGOA_TEST_CONNECTION`.

## Local logins

The admin account comes from `database/010_seed_admin.sql` (hashed password, same in every environment), so run
the database scripts before signing in as admin. Admins land on **Dashboard**, which lists every partner with
their vehicles and whether each is bookable right now. Partners sign up on the website and an admin approves them
under **Partners**; partners switch a vehicle on or off for bookings from **My vehicles**.

Roles live in the `Role` table (`database/006_role_table.sql`, renumbered by `007_renumber_roles.sql`): 1 Admin,
2 Partner, 3 Customer. They match the C# enum
`UserRole` and the role claim in the auth cookie.
