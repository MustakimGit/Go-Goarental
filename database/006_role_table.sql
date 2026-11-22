/*
    006 — Role lookup table.
    Users.Role keeps its TINYINT values (1 Customer, 2 Partner, 3 Admin) and now references Role.RoleId,
    so every role has a readable name in the database. Names match the C# enum UserRole and the auth role claim.
    CK_Users_Role stays: EnumCheckConstraintTests uses it to keep code and schema in step.
    Idempotent: safe to run again.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Role', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Role
    (
        RoleId      TINYINT        NOT NULL,
        Name        NVARCHAR(50)   NOT NULL,
        Description NVARCHAR(200)  NULL,
        CONSTRAINT PK_Role PRIMARY KEY (RoleId),
        CONSTRAINT UQ_Role_Name UNIQUE (Name)
    );
END;

MERGE dbo.Role AS target
USING (VALUES
    (CAST(1 AS TINYINT), N'Customer', N'Rents vehicles through the website.'),
    (CAST(2 AS TINYINT), N'Partner',  N'Rental partner: owns vehicles and rents them out through the platform.'),
    (CAST(3 AS TINYINT), N'Admin',    N'Runs the platform: approves partners and vehicles, manages catalog and pricing.')
) AS source (RoleId, Name, Description)
ON target.RoleId = source.RoleId
WHEN MATCHED THEN UPDATE SET Name = source.Name, Description = source.Description
WHEN NOT MATCHED THEN INSERT (RoleId, Name, Description) VALUES (source.RoleId, source.Name, source.Description);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Users_Role')
BEGIN
    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT FK_Users_Role FOREIGN KEY (Role) REFERENCES dbo.Role (RoleId);
END;

COMMIT TRANSACTION;
