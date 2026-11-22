/*
    003 — Reference data: vehicle categories, vehicle types and pickup locations.
    Idempotent: rows are inserted only if a row with the same name doesn't exist, so admin edits are kept.
    Coordinates are approximate; admins can correct them in the app.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.VehicleCategory (Name)
SELECT v.Name
FROM (VALUES (N'Two-wheeler'), (N'Four-wheeler')) AS v (Name)
WHERE NOT EXISTS (SELECT 1 FROM dbo.VehicleCategory c WHERE c.Name = v.Name);

INSERT INTO dbo.VehicleType (VehicleCategoryId, Name, Description, SeatingCapacity)
SELECT c.VehicleCategoryId, v.Name, v.Description, v.Seats
FROM (VALUES
    (N'Two-wheeler',  N'Scooter (Activa class)',         N'110–125cc automatic scooter. Honda Activa, TVS Jupiter or similar.', 2),
    (N'Two-wheeler',  N'Premium scooter',                N'125cc+ automatic scooter. TVS NTorq, Suzuki Burgman or similar.',  2),
    (N'Two-wheeler',  N'Cruiser motorcycle',             N'350cc motorcycle. Royal Enfield Classic 350 or similar.',          2),
    (N'Four-wheeler', N'Hatchback',                      N'Maruti Swift, Hyundai i20 or similar. Manual.',                    5),
    (N'Four-wheeler', N'Sedan',                          N'Honda City, Hyundai Verna or similar.',                            5),
    (N'Four-wheeler', N'Compact SUV',                    N'Maruti Brezza, Hyundai Venue or similar.',                         5),
    (N'Four-wheeler', N'7-seater SUV',                   N'Toyota Innova, Mahindra XUV700 or similar.',                       7)
) AS v (Category, Name, Description, Seats)
JOIN dbo.VehicleCategory c ON c.Name = v.Category
WHERE NOT EXISTS (SELECT 1 FROM dbo.VehicleType t WHERE t.Name = v.Name);

INSERT INTO dbo.PickupLocation (Name, Address, Latitude, Longitude, CustomerDeliveryFee)
SELECT v.Name, v.Address, v.Latitude, v.Longitude, v.Fee
FROM (VALUES
    (N'Panaji (Kadamba Bus Stand)',   N'Kadamba Bus Stand, Patto, Panaji, Goa 403001',          15.497500, 73.833700, 0),
    (N'Dabolim Airport (GOI)',        N'Goa International Airport, Dabolim, Goa 403801',         15.380800, 73.831400, 0),
    (N'Mopa Airport (GOX)',           N'Manohar International Airport, Mopa, Pernem, Goa 403512', 15.730000, 73.865000, 0),
    (N'Madgaon Railway Station',      N'Madgaon Railway Station, Margao, Goa 403601',            15.266900, 73.971700, 0),
    (N'Thivim Railway Station',       N'Thivim Railway Station, Thivim, Goa 403502',             15.619500, 73.879000, 0),
    (N'Calangute',                    N'Calangute Beach Road, Calangute, Goa 403516',            15.543900, 73.755300, 0),
    (N'Anjuna',                       N'Anjuna Beach Road, Anjuna, Goa 403509',                  15.573300, 73.741000, 0),
    (N'Palolem',                      N'Palolem Beach Road, Canacona, Goa 403702',               15.010000, 74.023200, 0)
) AS v (Name, Address, Latitude, Longitude, Fee)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PickupLocation p WHERE p.Name = v.Name);

COMMIT TRANSACTION;
