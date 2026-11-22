/*
    007 — Renumber roles: 1 Admin, 2 Partner, 3 Customer (was 1 Customer, 2 Partner, 3 Admin).
    Swaps 1 ⇄ 3 in Role and in every Users.Role value, together, so nobody changes role.
    CK_Users_Role (IN (1,2,3)) is unaffected. Must match the C# enum UserRole.
    Runs once: it only swaps while RoleId 1 is still named 'Customer', so re-running is a no-op.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM dbo.Role WITH (UPDLOCK, HOLDLOCK) WHERE RoleId = 1 AND Name = N'Customer')
BEGIN
    ALTER TABLE dbo.Users DROP CONSTRAINT FK_Users_Role;

    UPDATE dbo.Users
    SET Role = CASE Role WHEN 1 THEN 3 WHEN 3 THEN 1 ELSE Role END
    WHERE Role IN (1, 3);

    DELETE FROM dbo.Role;
    INSERT INTO dbo.Role (RoleId, Name, Description) VALUES
        (1, N'Admin',    N'Runs the platform: approves partners and vehicles, manages catalog and pricing.'),
        (2, N'Partner',  N'Rental partner: owns vehicles and rents them out through the platform.'),
        (3, N'Customer', N'Rents vehicles through the website.');

    ALTER TABLE dbo.Users WITH CHECK
        ADD CONSTRAINT FK_Users_Role FOREIGN KEY (Role) REFERENCES dbo.Role (RoleId);
END;

COMMIT TRANSACTION;
