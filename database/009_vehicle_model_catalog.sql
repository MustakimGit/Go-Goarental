/*
    009 — Common vehicle catalog.

    1. Vehicle types become: SUV, Hatchback, Sedan (four-wheelers) and Non Gear, Gear (two-wheelers).
       Existing types are renamed, not recreated, so their ids, prices and partner vehicles stay valid:
         Scooter (Activa class) -> Non Gear,  Cruiser motorcycle -> Gear,  Compact SUV -> SUV.
       Premium scooter and 7-seater SUV are set Inactive (kept for history); their vehicles move to Non Gear / SUV.
    2. New table dbo.Vehicle: the shared list of makes and models, each belonging to one vehicle type.
       Partners pick from this list instead of typing make/model, so every partner uses identical names.
    3. PartnerVehicle.VehicleId (required) links each physical vehicle to its catalog entry. Make, Model and
       VehicleTypeId stay on PartnerVehicle as copies the API fills from the catalog (readers don't change).
       Existing rows are matched by make + model; any without a match are added to the catalog first.

    Idempotent: safe to run again.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------- 1. Vehicle types */

DECLARE @two INT = (SELECT VehicleCategoryId FROM dbo.VehicleCategory WHERE Name = N'Two-wheeler');
DECLARE @four INT = (SELECT VehicleCategoryId FROM dbo.VehicleCategory WHERE Name = N'Four-wheeler');

-- Rename (old name -> new name) when the new name isn't taken yet.
UPDATE t
SET Name = v.NewName, Description = v.Description, SeatingCapacity = v.Seats, Status = 1
FROM dbo.VehicleType t
JOIN (VALUES
    (N'Scooter (Activa class)', N'Non Gear', N'Automatic scooter, no gears. Honda Activa, TVS Jupiter or similar.', CAST(2 AS TINYINT)),
    (N'Cruiser motorcycle',     N'Gear',     N'Geared motorcycle. Royal Enfield, Bajaj Pulsar or similar.',          CAST(2 AS TINYINT)),
    (N'Compact SUV',            N'SUV',      N'Compact and full-size SUVs. Hyundai Creta, Mahindra Thar or similar.', CAST(NULL AS TINYINT))
) AS v (OldName, NewName, Description, Seats) ON t.Name = v.OldName
WHERE NOT EXISTS (SELECT 1 FROM dbo.VehicleType x WHERE x.Name = v.NewName);

-- Create any of the five that still don't exist (e.g. on a database without the 003 seed).
INSERT INTO dbo.VehicleType (VehicleCategoryId, Name, Description, SeatingCapacity)
SELECT v.CategoryId, v.Name, v.Description, v.Seats
FROM (VALUES
    (@four, N'SUV',       N'Compact and full-size SUVs. Hyundai Creta, Mahindra Thar or similar.', CAST(NULL AS TINYINT)),
    (@four, N'Hatchback', N'Maruti Swift, Hyundai i20 or similar. Manual.',                        CAST(5 AS TINYINT)),
    (@four, N'Sedan',     N'Honda City, Hyundai Verna or similar.',                                CAST(5 AS TINYINT)),
    (@two,  N'Non Gear',  N'Automatic scooter, no gears. Honda Activa, TVS Jupiter or similar.',   CAST(2 AS TINYINT)),
    (@two,  N'Gear',      N'Geared motorcycle. Royal Enfield, Bajaj Pulsar or similar.',           CAST(2 AS TINYINT))
) AS v (CategoryId, Name, Description, Seats)
WHERE v.CategoryId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.VehicleType t WHERE t.Name = v.Name);

-- Retire the two types that no longer fit; move their vehicles to the matching new type.
UPDATE pv SET VehicleTypeId = target.VehicleTypeId, UpdatedAt = SYSUTCDATETIME()
FROM dbo.PartnerVehicle pv
JOIN dbo.VehicleType old ON old.VehicleTypeId = pv.VehicleTypeId
JOIN (VALUES (N'Premium scooter', N'Non Gear'), (N'7-seater SUV', N'SUV')) AS m (OldName, NewName) ON m.OldName = old.Name
JOIN dbo.VehicleType target ON target.Name = m.NewName;

UPDATE dbo.VehicleType SET Status = 2 WHERE Name IN (N'Premium scooter', N'7-seater SUV');

/* ---------------------------------------------------------------- 2. Vehicle catalog */

IF OBJECT_ID(N'dbo.Vehicle', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Vehicle   -- shared make/model list partners choose from
    (
        VehicleId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Vehicle PRIMARY KEY,
        VehicleTypeId   INT               NOT NULL,
        Make            NVARCHAR(100)     NOT NULL,
        Model           NVARCHAR(100)     NOT NULL,
        IsActive        BIT               NOT NULL CONSTRAINT DF_Vehicle_IsActive DEFAULT (1),
        CONSTRAINT UQ_Vehicle_MakeModel UNIQUE (Make, Model),
        CONSTRAINT FK_Vehicle_VehicleType FOREIGN KEY (VehicleTypeId) REFERENCES dbo.VehicleType (VehicleTypeId)
    );
    CREATE INDEX IX_Vehicle_VehicleType ON dbo.Vehicle (VehicleTypeId) INCLUDE (Make, Model, IsActive);
END;

INSERT INTO dbo.Vehicle (VehicleTypeId, Make, Model)
SELECT t.VehicleTypeId, v.Make, v.Model
FROM (VALUES
    -- SUV
    (N'SUV', N'Hyundai', N'Creta'),
    (N'SUV', N'Hyundai', N'Venue'),
    (N'SUV', N'Hyundai', N'Exter'),
    (N'SUV', N'Kia', N'Seltos'),
    (N'SUV', N'Kia', N'Sonet'),
    (N'SUV', N'Mahindra', N'Thar'),
    (N'SUV', N'Mahindra', N'Thar Roxx'),
    (N'SUV', N'Mahindra', N'Scorpio-N'),
    (N'SUV', N'Mahindra', N'XUV700'),
    (N'SUV', N'Mahindra', N'XUV 3XO'),
    (N'SUV', N'Mahindra', N'Bolero'),
    (N'SUV', N'Tata', N'Nexon'),
    (N'SUV', N'Tata', N'Punch'),
    (N'SUV', N'Tata', N'Harrier'),
    (N'SUV', N'Tata', N'Safari'),
    (N'SUV', N'Maruti Suzuki', N'Brezza'),
    (N'SUV', N'Maruti Suzuki', N'Grand Vitara'),
    (N'SUV', N'Maruti Suzuki', N'Fronx'),
    (N'SUV', N'Maruti Suzuki', N'Jimny'),
    (N'SUV', N'Maruti Suzuki', N'Ertiga'),
    (N'SUV', N'Toyota', N'Fortuner'),
    (N'SUV', N'Toyota', N'Innova Crysta'),
    (N'SUV', N'Toyota', N'Innova Hycross'),
    (N'SUV', N'Toyota', N'Urban Cruiser Hyryder'),
    (N'SUV', N'MG', N'Hector'),
    (N'SUV', N'MG', N'Astor'),
    (N'SUV', N'Renault', N'Kiger'),
    (N'SUV', N'Nissan', N'Magnite'),
    (N'SUV', N'Skoda', N'Kushaq'),
    (N'SUV', N'Volkswagen', N'Taigun'),
    -- Hatchback
    (N'Hatchback', N'Maruti Suzuki', N'Swift'),
    (N'Hatchback', N'Maruti Suzuki', N'Baleno'),
    (N'Hatchback', N'Maruti Suzuki', N'Wagon R'),
    (N'Hatchback', N'Maruti Suzuki', N'Alto K10'),
    (N'Hatchback', N'Maruti Suzuki', N'Celerio'),
    (N'Hatchback', N'Maruti Suzuki', N'Ignis'),
    (N'Hatchback', N'Maruti Suzuki', N'S-Presso'),
    (N'Hatchback', N'Hyundai', N'i20'),
    (N'Hatchback', N'Hyundai', N'Grand i10 Nios'),
    (N'Hatchback', N'Tata', N'Tiago'),
    (N'Hatchback', N'Tata', N'Altroz'),
    (N'Hatchback', N'Toyota', N'Glanza'),
    (N'Hatchback', N'Renault', N'Kwid'),
    (N'Hatchback', N'Citroen', N'C3'),
    -- Sedan
    (N'Sedan', N'Honda', N'City'),
    (N'Sedan', N'Honda', N'Amaze'),
    (N'Sedan', N'Hyundai', N'Verna'),
    (N'Sedan', N'Hyundai', N'Aura'),
    (N'Sedan', N'Maruti Suzuki', N'Dzire'),
    (N'Sedan', N'Maruti Suzuki', N'Ciaz'),
    (N'Sedan', N'Skoda', N'Slavia'),
    (N'Sedan', N'Volkswagen', N'Virtus'),
    (N'Sedan', N'Tata', N'Tigor'),
    (N'Sedan', N'Toyota', N'Camry'),
    -- Non Gear (scooters)
    (N'Non Gear', N'Honda', N'Activa 6G'),
    (N'Non Gear', N'Honda', N'Activa 5G'),
    (N'Non Gear', N'Honda', N'Activa 125'),
    (N'Non Gear', N'Honda', N'Dio'),
    (N'Non Gear', N'TVS', N'Jupiter'),
    (N'Non Gear', N'TVS', N'Jupiter 125'),
    (N'Non Gear', N'TVS', N'Ntorq 125'),
    (N'Non Gear', N'TVS', N'iQube'),
    (N'Non Gear', N'Suzuki', N'Access 125'),
    (N'Non Gear', N'Suzuki', N'Burgman Street'),
    (N'Non Gear', N'Yamaha', N'Fascino 125'),
    (N'Non Gear', N'Yamaha', N'RayZR 125'),
    (N'Non Gear', N'Yamaha', N'Aerox 155'),
    (N'Non Gear', N'Hero', N'Pleasure Plus'),
    (N'Non Gear', N'Hero', N'Maestro Edge 125'),
    (N'Non Gear', N'Hero', N'Xoom 110'),
    (N'Non Gear', N'Ola', N'S1 Pro'),
    (N'Non Gear', N'Ola', N'S1 Air'),
    (N'Non Gear', N'Ather', N'450X'),
    (N'Non Gear', N'Ather', N'Rizta'),
    (N'Non Gear', N'Bajaj', N'Chetak'),
    (N'Non Gear', N'Aprilia', N'SR 160'),
    -- Gear (motorcycles)
    (N'Gear', N'Royal Enfield', N'Classic 350'),
    (N'Gear', N'Royal Enfield', N'Bullet 350'),
    (N'Gear', N'Royal Enfield', N'Hunter 350'),
    (N'Gear', N'Royal Enfield', N'Meteor 350'),
    (N'Gear', N'Royal Enfield', N'Himalayan 450'),
    (N'Gear', N'Royal Enfield', N'Interceptor 650'),
    (N'Gear', N'Bajaj', N'Pulsar 150'),
    (N'Gear', N'Bajaj', N'Pulsar NS200'),
    (N'Gear', N'Bajaj', N'Avenger Cruise 220'),
    (N'Gear', N'Bajaj', N'Dominar 400'),
    (N'Gear', N'Honda', N'Shine 125'),
    (N'Gear', N'Honda', N'SP 125'),
    (N'Gear', N'Honda', N'CB350'),
    (N'Gear', N'Hero', N'Splendor Plus'),
    (N'Gear', N'Hero', N'Xpulse 200 4V'),
    (N'Gear', N'TVS', N'Apache RTR 160'),
    (N'Gear', N'TVS', N'Raider 125'),
    (N'Gear', N'Yamaha', N'FZ-S'),
    (N'Gear', N'Yamaha', N'MT-15'),
    (N'Gear', N'Yamaha', N'R15'),
    (N'Gear', N'KTM', N'Duke 200'),
    (N'Gear', N'KTM', N'Duke 390'),
    (N'Gear', N'Jawa', N'42'),
    (N'Gear', N'Triumph', N'Speed 400')
) AS v (TypeName, Make, Model)
JOIN dbo.VehicleType t ON t.Name = v.TypeName
WHERE NOT EXISTS (SELECT 1 FROM dbo.Vehicle x WHERE x.Make = v.Make AND x.Model = v.Model);

/* ---------------------------------------------------------------- 3. Link partner vehicles to the catalog */

IF COL_LENGTH(N'dbo.PartnerVehicle', N'VehicleId') IS NULL
BEGIN
    ALTER TABLE dbo.PartnerVehicle ADD VehicleId INT NULL;
END;

-- Dynamic SQL from here: the new column doesn't exist when the batch is compiled.
EXEC (N'
    -- Make + model a partner typed that isn''t in the catalog yet: add it, so no vehicle is left unlinked.
    INSERT INTO dbo.Vehicle (VehicleTypeId, Make, Model)
    SELECT MIN(pv.VehicleTypeId), LTRIM(RTRIM(pv.Make)), LTRIM(RTRIM(pv.Model))
    FROM dbo.PartnerVehicle pv
    WHERE pv.VehicleId IS NULL
      AND NOT EXISTS (SELECT 1 FROM dbo.Vehicle v WHERE v.Make = LTRIM(RTRIM(pv.Make)) AND v.Model = LTRIM(RTRIM(pv.Model)))
    GROUP BY LTRIM(RTRIM(pv.Make)), LTRIM(RTRIM(pv.Model));

    -- Link by make + model (case-insensitive collation), and adopt the catalog spelling and type.
    UPDATE pv
    SET VehicleId = v.VehicleId, Make = v.Make, Model = v.Model, VehicleTypeId = v.VehicleTypeId
    FROM dbo.PartnerVehicle pv
    JOIN dbo.Vehicle v ON v.Make = LTRIM(RTRIM(pv.Make)) AND v.Model = LTRIM(RTRIM(pv.Model))
    WHERE pv.VehicleId IS NULL;

    ALTER TABLE dbo.PartnerVehicle ALTER COLUMN VehicleId INT NOT NULL;
');

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PartnerVehicle_Vehicle')
BEGIN
    EXEC (N'ALTER TABLE dbo.PartnerVehicle WITH CHECK ADD CONSTRAINT FK_PartnerVehicle_Vehicle FOREIGN KEY (VehicleId) REFERENCES dbo.Vehicle (VehicleId);');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PartnerVehicle_Vehicle' AND object_id = OBJECT_ID(N'dbo.PartnerVehicle'))
BEGIN
    EXEC (N'CREATE INDEX IX_PartnerVehicle_Vehicle ON dbo.PartnerVehicle (VehicleId);');
END;

COMMIT TRANSACTION;
