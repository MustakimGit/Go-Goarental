/*
    010 — The platform admin account, for local and production databases alike.
    The app no longer creates an admin on startup; this script is the source of the admin login.

    PasswordHash is an ASP.NET Core Identity v3 hash (PBKDF2-HMAC-SHA512, 100,000 iterations, random salt),
    the same format the app writes for every account. The password itself is not stored here; changing it
    means generating a new hash in that format and updating Users.PasswordHash.

    Idempotent: the admin is inserted only if the email isn't registered yet, so re-running keeps any changes.
    Also retires the old development admin (admin@gogoarentals.local) where it exists, by marking it Deleted
    (Status 3) rather than removing the row, because past admin actions reference its UserId.
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Email = N'admin@gogoarental.in')
BEGIN
    INSERT INTO dbo.Users (Name, Email, Phone, PasswordHash, Role, Status)
    VALUES (
        N'Mustakim Shaikh',
        N'admin@gogoarental.in',
        NULL,
        N'AQAAAAIAAYagAAAAEPP7chNZRkQdQy+uLQe4steKMgPmd/3ajzFn0T2XpmkmV0E2/kwN4duEaCGwxooOdQ==',
        1,  -- Admin (see 007_renumber_roles.sql)
        1   -- Active
    );
END;

UPDATE dbo.Users
SET Status = 3, UpdatedAt = SYSUTCDATETIME()
WHERE Email = N'admin@gogoarentals.local' AND Status <> 3;

COMMIT TRANSACTION;
