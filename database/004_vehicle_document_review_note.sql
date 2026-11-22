/*
    004 — Admin's note on a vehicle document review, shown to the partner (e.g. why it was rejected).
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF COL_LENGTH('dbo.VehicleDocument', 'ReviewNote') IS NULL
BEGIN
    ALTER TABLE dbo.VehicleDocument ADD ReviewNote NVARCHAR(500) NULL;
END;

COMMIT TRANSACTION;
