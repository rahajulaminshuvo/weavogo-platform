/* =============================================================================
   Universal Item Master — 01_schema_platform.sql
   Identity & access tables.

   NOTE ON SCOPE: the SDS references UserAccount.UserId as the FK target of the
   standard audit columns (§4.1) and of RequestedBy / ActionedBy / ChangedBy
   (§8.2–§8.3), and Chapter 12 specifies a role-by-action security matrix, but it
   does not itself lay out these tables. They are defined here as the minimum
   surface the specified FKs and the Chapter 12 matrix require — no item-domain
   columns are invented.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   UserAccount
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.UserAccount', N'U') IS NULL
CREATE TABLE dbo.UserAccount
(
    UserId          INT IDENTITY(1,1)  NOT NULL,
    UserName        VARCHAR(60)        NOT NULL,
    FullName        NVARCHAR(150)      NOT NULL,
    Email           VARCHAR(255)       NOT NULL,
    PasswordHash    VARCHAR(255)       NOT NULL,
    IsSystemAdmin   BIT                NOT NULL DEFAULT (0),
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_UserAccount PRIMARY KEY CLUSTERED (UserId),
    CONSTRAINT UQ_UserAccount_UserName UNIQUE (UserName),
    CONSTRAINT UQ_UserAccount_Email UNIQUE (Email)
);
GO

/* --------------------------------------------------------------------------
   Role — the roles named in the Chapter 12 security matrix and in
   ApprovalStep.ApproverRole (§8.2.2).
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Role', N'U') IS NULL
CREATE TABLE dbo.Role
(
    RoleId          INT IDENTITY(1,1)  NOT NULL,
    RoleCode        VARCHAR(50)        NOT NULL,
    RoleName        VARCHAR(50)        NOT NULL,
    Description     VARCHAR(500)       NULL,
    IsScoped        BIT                NOT NULL DEFAULT (1),
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_Role PRIMARY KEY CLUSTERED (RoleId),
    CONSTRAINT UQ_Role_Code UNIQUE (RoleCode),
    CONSTRAINT UQ_Role_Name UNIQUE (RoleName)
);
GO

/* --------------------------------------------------------------------------
   UserRole
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.UserRole', N'U') IS NULL
CREATE TABLE dbo.UserRole
(
    UserRoleId      INT IDENTITY(1,1)  NOT NULL,
    UserId          INT                NOT NULL,
    RoleId          INT                NOT NULL,
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_UserRole PRIMARY KEY CLUSTERED (UserRoleId),
    CONSTRAINT FK_UserRole_User FOREIGN KEY (UserId) REFERENCES dbo.UserAccount (UserId),
    CONSTRAINT FK_UserRole_Role FOREIGN KEY (RoleId) REFERENCES dbo.Role (RoleId),
    CONSTRAINT UQ_UserRole_UserRole UNIQUE (UserId, RoleId)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '01_schema_platform.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('01_schema_platform.sql');
GO
