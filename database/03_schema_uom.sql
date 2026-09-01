/* =============================================================================
   Universal Item Master — 03_schema_uom.sql
   Chapter 5.4–5.5 — UOM dictionary and item-independent conversions.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §5.4 UOM
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.UOM', N'U') IS NULL
CREATE TABLE dbo.UOM
(
    UOMId              INT IDENTITY(1,1)  NOT NULL,
    UOMCode            VARCHAR(10)        NOT NULL,
    UOMName            VARCHAR(50)        NOT NULL,
    UOMType            VARCHAR(20)        NOT NULL,
    DecimalPrecision   TINYINT            NOT NULL DEFAULT (0),
    CreatedBy          INT                NOT NULL,
    CreatedDate        DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy         INT                NULL,
    ModifiedDate       DATETIME2(3)       NULL,
    IsActive           BIT                NOT NULL DEFAULT (1),
    IsDeleted          BIT                NOT NULL DEFAULT (0),
    RowVersion         ROWVERSION         NOT NULL,
    CONSTRAINT PK_UOM PRIMARY KEY CLUSTERED (UOMId),
    CONSTRAINT UQ_UOM_Code UNIQUE (UOMCode),
    CONSTRAINT CK_UOM_Type CHECK (UOMType IN ('Count','Weight','Length','Volume','Area'))
);
GO

/* --------------------------------------------------------------------------
   §5.5 UOMConversion
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.UOMConversion', N'U') IS NULL
CREATE TABLE dbo.UOMConversion
(
    UOMConversionId    INT IDENTITY(1,1)  NOT NULL,
    FromUOMId          INT                NOT NULL,
    ToUOMId            INT                NOT NULL,
    ConversionFactor   DECIMAL(18,6)      NOT NULL,
    CreatedBy          INT                NOT NULL,
    CreatedDate        DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy         INT                NULL,
    ModifiedDate       DATETIME2(3)       NULL,
    IsActive           BIT                NOT NULL DEFAULT (1),
    IsDeleted          BIT                NOT NULL DEFAULT (0),
    RowVersion         ROWVERSION         NOT NULL,
    CONSTRAINT PK_UOMConversion PRIMARY KEY CLUSTERED (UOMConversionId),
    CONSTRAINT FK_UOMConversion_From FOREIGN KEY (FromUOMId) REFERENCES dbo.UOM (UOMId),
    CONSTRAINT FK_UOMConversion_To   FOREIGN KEY (ToUOMId)   REFERENCES dbo.UOM (UOMId),
    CONSTRAINT UQ_UOMConversion_Pair UNIQUE (FromUOMId, ToUOMId),
    CONSTRAINT CK_UOMConversion_Factor CHECK (ConversionFactor > 0),
    CONSTRAINT CK_UOMConversion_NotSelf CHECK (FromUOMId <> ToUOMId)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '03_schema_uom.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('03_schema_uom.sql');
GO
