# GoGoaRentals — Architecture & Implementation Plan

Fixed-price vehicle rental marketplace for Goa. Customers book a vehicle *type* at a public price; the platform allocates a partner's vehicle automatically.

## 1. Environment (verified 2026-10-27)

| Tool | Status |
|---|---|
| .NET SDK | 8.0.427 (LTS) — installed |
| ASP.NET Core / .NET runtimes | 8.0.21, 9.0.10 |
| Node.js / npm | 22.20.0 / 8.19.2 |
| SQL Server | LocalDB `MSSQLLocalDB` — SQL Server 2019 Express (15.0.4382) |
| sqlcmd | ODBC 17 tools |
| EF Core / dotnet-ef | 8.0.33 (local tool, .config/dotnet-tools.json) |
| SSMS | not installed — to install via winget |

## 2. Solution layout

```
GoGoaRentals/
├─ GoGoaRentals.sln
├─ database/                      SSMS-managed SQL (source of truth)
│  ├─ 001_initial_schema.sql
│  ├─ 002_fix_one_to_many_unique_indexes.sql
│  ├─ 003_seed_reference_data.sql   categories, vehicle types, Goa pickup locations
│  ├─ 004_vehicle_document_review_note.sql
│  ├─ 005_seed_starting_prices.sql  placeholder daily prices (edit under Admin → Pricing)
│  └─ NNN_<change>.sql            incremental changes, never edited once applied
├─ docs/                          PLAN.md, SCAFFOLDING.md (incl. enum mapping)
├─ src/
│  ├─ Client/                         React + Vite + Tailwind + shadcn/ui (JS/JSX)
│  ├─ Server/                         ASP.NET Core Web API (startup project)
│  │  ├─ Controllers/  WebModels/  Middleware/  Configuration/
│  │  ├─ BackgroundJobs/          hosted services: outbox worker, reservation expiry, payment reconciliation
│  │  ├─ Hubs/                    SignalR
│  │  └─ appsettings.json / appsettings.Development.json
│  ├─ Entities/                       scaffold target + domain types (no business logic)
│  │  ├─ Data/AppDbContext.cs     GENERATED — do not edit
│  │  ├─ Data/AppDbContext.Partial.cs   custom config via OnModelCreatingPartial
│  │  ├─ Models/*.cs              GENERATED — do not edit
│  │  ├─ Models/Partials/*.cs     partial classes: typed enum accessors, helpers
│  │  └─ Enums/*.cs
│  └─ Services/                       business logic
│     ├─ Interfaces/  Services/  Allocation/  Payments/  Notifications/
│     └─ Repositories/            only if one earns its place
└─ tests/
   ├─ Tests.Unit/
   └─ Tests.Integration/   real SQL Server (LocalDB), incl. concurrency tests
```

Project names are `Client`, `Server`, `Entities`, `Services`; root namespaces are `GoGoaRentals.<Project>`.
Dependency direction: `Server → Services → Entities`. The client talks only to `Server` over HTTP.

## 3. Key decisions

**Database-first.** Schema changes are made in SSMS / `database/*.sql`, then entities are re-scaffolded with
`--force` into `Entities/Models` and `Entities/Data/AppDbContext.cs` only. Custom code lives in partial classes so regeneration never loses it.
EF Core migrations are not used. Full command in `docs/SCAFFOLDING.md`.

**Connection string.** `appsettings.Development.json` in Server holds the LocalDB connection
string (Windows auth — no password in the file). Production connection strings and all Razorpay/email/SMS
secrets come from environment variables / a secret store, never committed. Scaffolding reads
`Name=ConnectionStrings:DefaultConnection`, so the string is never on the command line.

**Enums.** Stable states are `TINYINT` + `CHECK` constraint in SQL and `enum : byte` with explicit values in C#.
Scaffolding generates `byte` properties; typed accessors live in partial classes, e.g.
`[NotMapped] public BookingStatus Status { get => (BookingStatus)BookingStatus; ... }` with validation of
unknown values. Admin-managed values (vehicle categories, locations, pricing) are tables. Frontend gets
enum values + labels from a `GET /api/meta/enums` endpoint, so there is one source of truth.

**Time.** All instants stored in UTC (`datetime2(0)`). Rental-day boundaries computed in IST. Intervals are
`[StartAt, EndAt)`. Turnaround buffer (config `Allocation:TurnaroundBufferMinutes`) applied at query time.

**Double-booking protection.** Inside one SQL transaction per candidate:
`sp_getapplock 'vehicle:{id}'` (exclusive, transaction-owned) → overlap check
(`ExistingStartAt < ReqEnd + buffer AND ExistingEndAt > ReqStart - buffer` over active blocks) → insert
`VehicleCalendarBlock` (Reservation, Active, `ExpiresAt` = payment hold) + `BookingAssignment` → commit.
Maintenance and partner blocks take the same lock. No external calls inside the transaction.

**Booking flow.** Quote (backend price) → `POST /api/bookings` with idempotency key → allocate and hold
(status `AwaitingPayment`) → create Razorpay order for the snapshotted total → verify signature + fetch
payment from Razorpay (amount, currency, order) → `Confirmed`, reservation `Consumed`, outbox events written
in the same transaction. Expired unpaid holds are released by a background job only after reconciling with
Razorpay.

**Auth.** ASP.NET Core Identity password hashing + HttpOnly, Secure, SameSite cookie auth (no tokens in
localStorage). Roles from the database only. Partner data access always filtered by the caller's
`RentalPartnerId`.

**Notifications.** Transactional outbox → hosted worker → per-channel providers behind interfaces
(`IEmailNotificationProvider`, `ISmsNotificationProvider`, `IWhatsAppNotificationProvider`,
`IRealtimeNotificationService`). Exponential backoff + jitter; idempotency key per (event, channel, recipient).
Dev uses logging/no-op providers until real credentials are supplied.

## 4. Schema summary (`database/001_initial_schema.sql`)

22 tables, 31 FKs, 48 CHECK constraints. Verified on LocalDB.
Additions beyond the brief, and why:

| Addition | Reason |
|---|---|
| `VehicleCategory` table | Admin-managed, so not an enum |
| `VehicleDocument` | Eligibility needs per-document expiry (insurance, RC, PUC, permit) |
| `UserNotificationPreference` | Consent / opt-out, required for WhatsApp |
| `PickupLocation.CustomerDeliveryFee` vs `PartnerServiceArea.DeliveryCost` | Public fee stays fixed; partner cost feeds ranking and settlement |
| `Booking.PickupMode`, `DeliveryAddress`, `DepositAmount`, `RowVersion` | Delivery support, deposit in snapshot, safe state transitions |
| `VehicleCalendarBlock.ExpiresAt` | Payment-hold expiry for unpaid reservations |
| `NotificationOutbox.LockedUntil`, `Notification.IdempotencyKey` | Worker lease and duplicate prevention |
| `UX_BookingAssignment_OneReserved`, `UX_VehicleCalendarBlock_BookingActive` | DB-level guarantee: one live reservation per booking |

## 5. Phases

1. ✅ Environment check, architecture, initial schema (this document)
   ✅ Solution + projects, LocalDB database, scaffolded entities, enums, health check, schema-drift tests
2. ✅ Auth & roles, partners, vehicle types, individual inventory (see §7)
3. ✅ Pricing engine + snapshots, availability, vehicle calendars (see §8)
4. Allocation service, atomic reservations, idempotency, booking state machine
5. Razorpay orders, verification, webhooks, refunds, reconciliation
6. Outbox worker, email/SMS/WhatsApp providers, SignalR
7. Customer, partner, admin UIs
8. Test hardening (concurrency, webhooks, authz), deployment and backup docs

Each phase ends with a build and passing tests before moving on.

## 6. Open business questions (needed before Phase 3/5)

1. ~~**Rental units:**~~ **Decided 2026-10-31: daily only.** Any part of a rental day is charged as a full day.
2. ~~**Daily boundary:**~~ **Decided 2026-10-31: rental days run 9 am to 9 am IST** (`BookingPolicy:RentalDayStartHour`).
3. **Deposit:** collected online with the rental, or at handover by the partner? Refunded by whom?
4. **Taxes:** GST rate and whether public prices include tax.
5. **Cancellation policy:** refund % by time-before-pickup.
6. **Payment hold:** how long a reservation is held while awaiting payment (suggest 15 minutes).
7. **Turnaround buffer:** default gap between rentals of the same vehicle (suggest 60 minutes).
8. **Settlement:** net payout = gross − commission − deductions, with partner delivery cost reimbursed or not? Is commission charged on the delivery fee?
9. **Reliability threshold:** minimum partner score to receive bookings (suggest 60/100).

## 7. Phase 2 notes

- **Login:** cookie auth (`ggr.auth`, HttpOnly, SameSite=Lax, Secure outside dev). Every request re-checks the user is
  still active and their role/partner id unchanged (`OnValidatePrincipal`), so suspensions apply immediately.
  Development seeds an admin from `DevSeed` in `appsettings.Development.json`.
- **Partners:** register → `PendingApproval` → admin approves (`Active`) or rejects. Active ⇄ Suspended; Rejected can
  be approved later. Only `Active` partners can add or switch on vehicles. Admin actions are written to `AuditLog`.
- **Vehicles:** a vehicle needs a verified, unexpired RC, insurance, PUC and rental permit to go live
  (`VehicleVerification`). A pending renewal doesn't take a live vehicle offline while an older verified copy is
  still valid. Status moves PendingVerification ⇄ Active automatically; Inactive (paused) and Retired are the partner's choice.
- **Documents:** PDF/JPG/PNG up to 5 MB, checked by file signature, stored outside the web root
  (`Storage:LocalRoot`, default `src/Server/App_Data/uploads`) and served only through authorized endpoints.
  Swap `LocalFileStorage` for blob storage in production.
- **Later phases:** a background job should re-evaluate document expiry daily (Phase 6); retiring a vehicle must
  check upcoming bookings (Phase 4); allocation must filter on partner `Active` + vehicle `Active`.

## 8. Phase 3 notes

- **Rental days:** 9 am → 9 am Goa time. A rental is charged for every rental day it touches
  (`RentalDays.Between`): 9 Nov 09:00 → 10 Nov 09:00 = 1 day; 14:00 → next day 11:00 = 2 days; 08:00 → 18:00 the same
  day = 2 days (8 am belongs to the previous rental day). The search form defaults to 9 am pickup and return.
- **Prices:** `VehiclePricing` rows with `DurationType = Daily`, stored as UTC instants at rental-day starts and shown
  to admins as first/last day. Each rental day is priced at the rate in effect when that day starts, so seasonal rates
  work per day. Adding a rate splits an earlier rate around it (the earlier rate resumes after a bounded rate);
  other overlaps are refused. Withdrawn rates are kept for history.
- **Quote** (`QuoteService`): rental + delivery fee (pickup location's public fee, if delivery) + tax (per day's
  `TaxPercent`, 0 until GST is decided) + deposit (first day's rate) = total, matching `CK_Booking_Amounts`. The
  `Quote` object is what Phase 4 stores as `Booking.PricingSnapshot`. Limits: pickup ≥ 60 min ahead, ≤ 180 days
  ahead, ≤ 30 rental days (`BookingPolicy`).
- **Availability:** a vehicle counts if it is live, verified, its partner and type are active, it is **based at the
  pickup location**, and no blocking calendar entry overlaps the period ± the 60-minute turnaround buffer.
  Partner service areas (serving other locations / delivery radius) are not used yet.
- **Partner calendar:** partners block maintenance / unavailable time per vehicle under the same per-vehicle app
  lock that Phase 4 reservations will use; blocks can't overlap customer reservations.
- **Open:** deposit collection (online vs at handover), GST, cancellation policy, payment hold (15 min assumed).
