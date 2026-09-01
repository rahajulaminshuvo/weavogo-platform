/* =============================================================================
   Universal Item Master — 04_schema_classification.sql
   Chapter 4.3–4.9 — classification hierarchy and the dynamic attribute engine.
   DDL for §4.3–4.6 and §4.7–4.9 is reproduced verbatim from §10.9.1–10.9.7.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §4.7 / §10.9.5 AttributeDefinition — the shared attribute dictionary
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AttributeDefinition', N'U') IS NULL
CREATE TABLE dbo.AttributeDefinition
(
    AttributeDefinitionId   INT IDENTITY(1,1)  NOT NULL,
    AttributeCode           VARCHAR(50)        NOT NULL,
    AttributeName           VARCHAR(100)       NOT NULL,
    DataType                VARCHAR(20)        NOT NULL,
    UnitOfMeasure           VARCHAR(20)        NULL,
    EnumOptions             NVARCHAR(MAX)      NULL,
    CreatedBy               INT                NOT NULL,
    CreatedDate             DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy              INT                NULL,
    ModifiedDate            DATETIME2(3)       NULL,
    IsActive                BIT                NOT NULL DEFAULT (1),
    IsDeleted               BIT                NOT NULL DEFAULT (0),
    RowVersion              ROWVERSION         NOT NULL,
    CONSTRAINT PK_AttributeDefinition PRIMARY KEY CLUSTERED (AttributeDefinitionId),
    CONSTRAINT UQ_AttributeDefinition_Code UNIQUE (AttributeCode),
    CONSTRAINT CK_AttributeDefinition_DataType
        CHECK (DataType IN ('Text','Number','Boolean','Date','Enum')),
    CONSTRAINT CK_AttributeDefinition_EnumOptions
        CHECK (DataType <> 'Enum' OR EnumOptions IS NOT NULL)
);
GO

/* --------------------------------------------------------------------------
   §4.8 / §10.9.6 AttributeTemplate
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AttributeTemplate', N'U') IS NULL
CREATE TABLE dbo.AttributeTemplate
(
    AttributeTemplateId   INT IDENTITY(1,1)  NOT NULL,
    TemplateCode          VARCHAR(30)        NOT NULL,
    TemplateName          VARCHAR(100)       NOT NULL,
    Description           VARCHAR(500)       NULL,
    CreatedBy             INT                NOT NULL,
    CreatedDate           DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy            INT                NULL,
    ModifiedDate          DATETIME2(3)       NULL,
    IsActive              BIT                NOT NULL DEFAULT (1),
    IsDeleted             BIT                NOT NULL DEFAULT (0),
    RowVersion            ROWVERSION         NOT NULL,
    CONSTRAINT PK_AttributeTemplate PRIMARY KEY CLUSTERED (AttributeTemplateId),
    CONSTRAINT UQ_AttributeTemplate_Code UNIQUE (TemplateCode)
);
GO

/* --------------------------------------------------------------------------
   §4.9 / §10.9.7 TemplateAttribute
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TemplateAttribute', N'U') IS NULL
CREATE TABLE dbo.TemplateAttribute
(
    TemplateAttributeId     INT IDENTITY(1,1)  NOT NULL,
    AttributeTemplateId     INT                NOT NULL,
    AttributeDefinitionId   INT                NOT NULL,
    IsRequired              BIT                NOT NULL DEFAULT (1),
    DisplayOrder            INT                NOT NULL DEFAULT (0),
    DefaultValue            NVARCHAR(200)      NULL,
    CreatedBy               INT                NOT NULL,
    CreatedDate             DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy              INT                NULL,
    ModifiedDate            DATETIME2(3)       NULL,
    IsActive                BIT                NOT NULL DEFAULT (1),
    IsDeleted               BIT                NOT NULL DEFAULT (0),
    RowVersion              ROWVERSION         NOT NULL,
    CONSTRAINT PK_TemplateAttribute PRIMARY KEY CLUSTERED (TemplateAttributeId),
    CONSTRAINT FK_TemplateAttribute_Template FOREIGN KEY (AttributeTemplateId)
        REFERENCES dbo.AttributeTemplate (AttributeTemplateId),
    CONSTRAINT FK_TemplateAttribute_Definition FOREIGN KEY (AttributeDefinitionId)
        REFERENCES dbo.AttributeDefinition (AttributeDefinitionId),
    CONSTRAINT UQ_TemplateAttribute_TemplateDefinition
        UNIQUE (AttributeTemplateId, AttributeDefinitionId)
);
GO

/* --------------------------------------------------------------------------
   §4.3 / §10.9.1 ItemCategory
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemCategory', N'U') IS NULL
CREATE TABLE dbo.ItemCategory
(
    ItemCategoryId   INT IDENTITY(1,1)  NOT NULL,
    CategoryCode     VARCHAR(10)        NOT NULL,
    CategoryName     VARCHAR(100)       NOT NULL,
    Nature           VARCHAR(20)        NOT NULL,
    Description      VARCHAR(500)       NULL,
    DisplayOrder     INT                NOT NULL DEFAULT (0),
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemCategory PRIMARY KEY CLUSTERED (ItemCategoryId),
    CONSTRAINT UQ_ItemCategory_Code UNIQUE (CategoryCode),
    CONSTRAINT CK_ItemCategory_Nature CHECK (Nature IN ('Material','Goods','Asset','Service')),
    /* §12.1 rule 1 — 2–10 chars, uppercase A–Z / 0–9 only, no spaces. */
    CONSTRAINT CK_ItemCategory_CodeFormat
        CHECK (LEN(CategoryCode) BETWEEN 2 AND 10
               AND CategoryCode COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%')
);
GO

/* --------------------------------------------------------------------------
   §4.4 / §10.9.2 ItemGroup
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemGroup', N'U') IS NULL
CREATE TABLE dbo.ItemGroup
(
    ItemGroupId      INT IDENTITY(1,1)  NOT NULL,
    ItemCategoryId   INT                NOT NULL,
    GroupCode        VARCHAR(10)        NOT NULL,
    GroupName        VARCHAR(100)       NOT NULL,
    Description      VARCHAR(500)       NULL,
    DisplayOrder     INT                NOT NULL DEFAULT (0),
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemGroup PRIMARY KEY CLUSTERED (ItemGroupId),
    CONSTRAINT FK_ItemGroup_Category FOREIGN KEY (ItemCategoryId)
        REFERENCES dbo.ItemCategory (ItemCategoryId),
    CONSTRAINT UQ_ItemGroup_CategoryCode UNIQUE (ItemCategoryId, GroupCode),
    CONSTRAINT CK_ItemGroup_CodeFormat
        CHECK (LEN(GroupCode) BETWEEN 2 AND 10
               AND GroupCode COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%')
);
GO

/* --------------------------------------------------------------------------
   §4.5 / §10.9.3 ItemSubGroup
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemSubGroup', N'U') IS NULL
CREATE TABLE dbo.ItemSubGroup
(
    ItemSubGroupId   INT IDENTITY(1,1)  NOT NULL,
    ItemGroupId      INT                NOT NULL,
    SubGroupCode     VARCHAR(10)        NOT NULL,
    SubGroupName     VARCHAR(100)       NOT NULL,
    Description      VARCHAR(500)       NULL,
    DisplayOrder     INT                NOT NULL DEFAULT (0),
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemSubGroup PRIMARY KEY CLUSTERED (ItemSubGroupId),
    CONSTRAINT FK_ItemSubGroup_Group FOREIGN KEY (ItemGroupId)
        REFERENCES dbo.ItemGroup (ItemGroupId),
    CONSTRAINT UQ_ItemSubGroup_GroupCode UNIQUE (ItemGroupId, SubGroupCode),
    CONSTRAINT CK_ItemSubGroup_CodeFormat
        CHECK (LEN(SubGroupCode) BETWEEN 2 AND 10
               AND SubGroupCode COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9]%')
);
GO

/* --------------------------------------------------------------------------
   §4.6 / §10.9.4 ItemFamily
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemFamily', N'U') IS NULL
CREATE TABLE dbo.ItemFamily
(
    ItemFamilyId                 INT IDENTITY(1,1)  NOT NULL,
    ItemSubGroupId               INT                NOT NULL,
    FamilyCode                   VARCHAR(10)        NOT NULL,
    FamilyName                   VARCHAR(100)       NOT NULL,
    DefaultAttributeTemplateId   INT                NULL,
    DisplayOrder                 INT                NOT NULL DEFAULT (0),
    CreatedBy                    INT                NOT NULL,
    CreatedDate                  DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy                   INT                NULL,
    ModifiedDate                 DATETIME2(3)       NULL,
    IsActive                     BIT                NOT NULL DEFAULT (1),
    IsDeleted                    BIT                NOT NULL DEFAULT (0),
    RowVersion                   ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemFamily PRIMARY KEY CLUSTERED (ItemFamilyId),
    CONSTRAINT FK_ItemFamily_SubGroup FOREIGN KEY (ItemSubGroupId)
        REFERENCES dbo.ItemSubGroup (ItemSubGroupId),
    CONSTRAINT FK_ItemFamily_Template FOREIGN KEY (DefaultAttributeTemplateId)
        REFERENCES dbo.AttributeTemplate (AttributeTemplateId),
    CONSTRAINT UQ_ItemFamily_SubGroupCode UNIQUE (ItemSubGroupId, FamilyCode)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '04_schema_classification.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('04_schema_classification.sql');
GO
