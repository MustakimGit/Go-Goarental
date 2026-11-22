/*
    005 — Starting daily prices for the vehicle types seeded in 003, so search works out of the box.
    These are placeholders: review them in the app under Admin → Pricing.
    Only types with no daily price at all get one, so re-running never overrides admin changes.
    Prices start from the rental day beginning 1 Oct 2026, 09:00 IST (03:30 UTC). Tax is 0 until GST is decided.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.VehiclePricing (VehicleTypeId, DurationType, CustomerPrice, DepositAmount, TaxPercent, EffectiveFrom)
SELECT t.VehicleTypeId, 2 /* Daily */, v.Price, v.Deposit, 0, '2026-10-01T03:30:00'
FROM (VALUES
    (N'Scooter (Activa class)',  400.00, 1000.00),
    (N'Premium scooter',         600.00, 1500.00),
    (N'Cruiser motorcycle',     1200.00, 3000.00),
    (N'Hatchback',              1800.00, 5000.00),
    (N'Sedan',                  2500.00, 5000.00),
    (N'Compact SUV',            3000.00, 7500.00),
    (N'7-seater SUV',           4000.00, 10000.00)
) AS v (TypeName, Price, Deposit)
JOIN dbo.VehicleType t ON t.Name = v.TypeName
WHERE NOT EXISTS (SELECT 1 FROM dbo.VehiclePricing p WHERE p.VehicleTypeId = t.VehicleTypeId AND p.DurationType = 2);

COMMIT TRANSACTION;
