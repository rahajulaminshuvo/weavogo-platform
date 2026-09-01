/* =============================================================================
   Universal Item Master — 02_schema_organization.sql
   Chapter 5.2 — Company, BusinessUnit (+ UserBusinessUnit, the scoping table the
   Chapter 12 "Yes (scoped)" permissions are evaluated against).
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §5.2.1 Company
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
CREATE TABLE dbo.Company
(
    CompanyId           INT IDENTITY(1,1)  NOT NULL,
    CompanyCode         VARCHAR(15)        NOT NULL,
    CompanyName         VARCHAR(200)       NOT NULL,
    TaxRegistrationNo   VARCHAR(30)        NULL,
    Country             VARCHAR(60)        NOT NULL,
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_Company PRIMARY KEY CLUSTERED (CompanyId),
    CONSTRAINT UQ_Company_Code UNIQUE (CompanyCode)
);
GO

/* --------------------------------------------------------------------------
   §5.2.2 BusinessUnit
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BusinessUnit', N'U') IS NULL
CREATE TABLE dbo.BusinessUnit
(
    BusinessUnitId  INT IDENTITY(1,1)  NOT NULL,
    CompanyId       INT                NOT NULL,
    UnitCode        VARCHAR(15)        NOT NULL,
    UnitName        VARCHAR(150)       NOT NULL,
    BusinessType    VARCHAR(50)        NOT NULL,
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_BusinessUnit PRIMARY KEY CLUSTERED (BusinessUnitId),
    CONSTRAINT FK_BusinessUnit_Company FOREIGN KEY (CompanyId) REFERENCES dbo.Company (CompanyId),
    CONSTRAINT UQ_BusinessUnit_CompanyCode UNIQUE (CompanyId, UnitCode)
);
GO

/* --------------------------------------------------------------------------
   UserBusinessUnit — §12.3 "scoped" permissions apply only within the user's
   own BusinessUnitId; this is the table that defines "own".
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.UserBusinessUnit', N'U') IS NULL
CREATE TABLE dbo.UserBusinessUnit
(
    UserBusinessUnitId  INT IDENTITY(1,1)  NOT NULL,
    UserId              INT                NOT NULL,
    BusinessUnitId      INT                NOT NULL,
    IsPrimary           BIT                NOT NULL DEFAULT (0),
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_UserBusinessUnit PRIMARY KEY CLUSTERED (UserBusinessUnitId),
    CONSTRAINT FK_UserBusinessUnit_User FOREIGN KEY (UserId) REFERENCES dbo.UserAccount (UserId),
    CONSTRAINT FK_UserBusinessUnit_Unit FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    CONSTRAINT UQ_UserBusinessUnit_UserUnit UNIQUE (UserId, BusinessUnitId)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '02_schema_organization.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('02_schema_organization.sql');
GO
