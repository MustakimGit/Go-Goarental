/*
    008 — More pickup locations at well-known Goa destinations, alongside the airports, stations and towns seeded in 003.
    Idempotent: rows are inserted only if a row with the same name doesn't exist, so admin edits are kept.
    Coordinates are approximate; admins can correct them in the app.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

INSERT INTO dbo.PickupLocation (Name, Address, Latitude, Longitude, CustomerDeliveryFee)
SELECT v.Name, v.Address, v.Latitude, v.Longitude, v.Fee
FROM (VALUES
    -- North Goa
    (N'Baga Beach',                   N'Baga Beach Road, Baga, Calangute, Goa 403516',           15.556000, 73.751600, 0),
    (N'Candolim',                     N'Fort Aguada Road, Candolim, Goa 403515',                 15.518100, 73.762300, 0),
    (N'Fort Aguada',                  N'Fort Aguada, Sinquerim, Candolim, Goa 403515',           15.492300, 73.773400, 0),
    (N'Vagator (Chapora Fort)',       N'Chapora Fort Road, Vagator, Goa 403509',                 15.605700, 73.736100, 0),
    (N'Morjim Beach',                 N'Morjim Beach Road, Morjim, Pernem, Goa 403512',          15.630000, 73.735000, 0),
    (N'Arambol Beach',                N'Arambol Beach Road, Arambol, Pernem, Goa 403524',        15.686800, 73.704400, 0),
    (N'Mapusa Market',                N'Mapusa Municipal Market, Mapusa, Goa 403507',            15.591500, 73.809700, 0),
    -- Central Goa
    (N'Miramar Beach',                N'Miramar Beach, Panaji, Goa 403001',                      15.481700, 73.808000, 0),
    (N'Dona Paula',                   N'Dona Paula Jetty, Dona Paula, Goa 403004',               15.454600, 73.804700, 0),
    (N'Old Goa (Basilica of Bom Jesus)', N'Basilica of Bom Jesus, Old Goa, Goa 403402',          15.500900, 73.911600, 0),
    (N'Vasco da Gama',                N'Vasco da Gama Railway Station, Vasco da Gama, Goa 403802', 15.398800, 73.815300, 0),
    -- South Goa
    (N'Colva Beach',                  N'Colva Beach Road, Colva, Goa 403708',                    15.279500, 73.910600, 0),
    (N'Benaulim Beach',               N'Benaulim Beach Road, Benaulim, Goa 403716',              15.254300, 73.921300, 0),
    (N'Majorda Beach',                N'Majorda Beach Road, Majorda, Goa 403713',                15.311600, 73.901900, 0),
    (N'Agonda Beach',                 N'Agonda Beach Road, Agonda, Canacona, Goa 403702',        15.045200, 73.986400, 0)
) AS v (Name, Address, Latitude, Longitude, Fee)
WHERE NOT EXISTS (SELECT 1 FROM dbo.PickupLocation p WHERE p.Name = v.Name);

COMMIT TRANSACTION;
