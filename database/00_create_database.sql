/* =============================================================================
   Universal Item Master — 00_create_database.sql
   Creates the WeavoItemMaster database.
   Safe to re-run: creation is guarded, existing databases are left untouched.
   ============================================================================= */
SET NOCOUNT ON;
GO

IF DB_ID(N'WeavoItemMaster') IS NULL
BEGIN
    PRINT 'Creating database WeavoItemMaster ...';
    EXEC (N'CREATE DATABASE WeavoItemMaster');
END
ELSE
    PRINT 'Database WeavoItemMaster already exists — skipping creation.';
GO

ALTER DATABASE WeavoItemMaster SET RECOVERY SIMPLE;
GO

USE WeavoItemMaster;
GO

/* Migration bookkeeping — every script in this folder records itself here so the
   migration runner is idempotent against a database that is already partly built. */
IF OBJECT_ID(N'dbo.SchemaMigration', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigration
    (
        MigrationId   INT IDENTITY(1,1)  NOT NULL,
        ScriptName    VARCHAR(200)       NOT NULL,
        AppliedDate   DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_SchemaMigration PRIMARY KEY CLUSTERED (MigrationId),
        CONSTRAINT UQ_SchemaMigration_ScriptName UNIQUE (ScriptName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '00_create_database.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('00_create_database.sql');
GO
