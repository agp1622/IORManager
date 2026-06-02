-- ============================================================
-- IORManager — Users table setup & seed
-- ============================================================
-- HOW TO USE:
--   Option A (preferred): Run the EF migration first, then only
--     execute the INSERT block below (section 2).
--
--     cd backend
--     dotnet ef migrations add AddUsers
--     dotnet ef database update
--
--   Option B: Run this entire script directly in SQL Server
--     if you prefer not to use EF migrations.
-- ============================================================

-- 1. Create the Users table (skip if you used EF migrations)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users')
BEGIN
    CREATE TABLE Users (
        Id           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        Email        NVARCHAR(200)    NOT NULL,
        PasswordHash NVARCHAR(MAX)    NOT NULL,
        Role         NVARCHAR(50)     NOT NULL DEFAULT 'User',
        Name         NVARCHAR(200)    NOT NULL,
        CreatedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );
    PRINT 'Users table created.';
END
ELSE
    PRINT 'Users table already exists.';

-- ============================================================
-- 2. Seed default users
--
-- Default credentials (change after first login!):
--   admin@ior.com    /  Admin1234!
--   manager@ior.com  /  Manager1234!
--   user@ior.com     /  User1234!
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@ior.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, Role, Name, CreatedAt) VALUES (
        NEWID(),
        'admin@ior.com',
        '$2b$11$1mim7cF36qGlLESuHjoHC.tASf/cWCzYYqHHIa4WrOiUoJVGGSaC.',
        'Admin',
        'Administrador',
        GETUTCDATE()
    );
    PRINT 'Admin user inserted.';
END

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'manager@ior.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, Role, Name, CreatedAt) VALUES (
        NEWID(),
        'manager@ior.com',
        '$2b$11$bhaxG2xK7F6TPSsU9RJeseQHp0SDp8vlNpRN0BAZW3D9oN.KaJgG2',
        'Manager',
        'Gerente',
        GETUTCDATE()
    );
    PRINT 'Manager user inserted.';
END

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'user@ior.com')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, Role, Name, CreatedAt) VALUES (
        NEWID(),
        'user@ior.com',
        '$2b$11$rlc8m.S8zRMuoaguSE3KHODqsE6qtncqEw0hwVaObv.7bt64y8L76',
        'User',
        'Usuario',
        GETUTCDATE()
    );
    PRINT 'User inserted.';
END
GO
