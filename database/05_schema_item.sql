/* =============================================================================
   Universal Item Master — 05_schema_item.sql
   §4.10 ItemMaster, §4.11 ItemAttribute, §5.3 BusinessUnitItem, §5.6 ItemPackaging.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §4.10 / §10.9.8 ItemMaster
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemMaster', N'U') IS NULL
CREATE TABLE dbo.ItemMaster
(
    ItemId                 BIGINT IDENTITY(1,1)  NOT NULL,
    ItemCode               VARCHAR(30)           NOT NULL,
    ItemName               NVARCHAR(200)         NOT NULL,
    Description            NVARCHAR(1000)        NULL,
    ItemCategoryId         INT                   NOT NULL,   -- denormalized ancestry (§4.2)
    ItemGroupId            INT                   NOT NULL,   -- denormalized ancestry
    ItemSubGroupId         INT                   NOT NULL,   -- denormalized ancestry
    ItemFamilyId           INT                   NOT NULL,   -- direct parent
    AttributeTemplateId    INT                   NULL,
    BaseUOMId              INT                   NOT NULL,
    CanPurchase            BIT                   NOT NULL DEFAULT (1),
    CanSell                BIT                   NOT NULL DEFAULT (0),
    CanManufacture         BIT                   NOT NULL DEFAULT (0),
    CanStock               BIT                   NOT NULL DEFAULT (1),
    CanTransfer            BIT                   NOT NULL DEFAULT (1),
    IsSerialControlled     BIT                   NOT NULL DEFAULT (0),
    IsLotControlled        BIT                   NOT NULL DEFAULT (0),
    ItemStatus             VARCHAR(20)           NOT NULL DEFAULT ('Draft'),
    VersionNumber          INT                   NOT NULL DEFAULT (1),
    CreatedBy              INT                   NOT NULL,
    CreatedDate            DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy             INT                   NULL,
    ModifiedDate           DATETIME2(3)          NULL,
    IsActive               BIT                   NOT NULL DEFAULT (1),
    IsDeleted              BIT                   NOT NULL DEFAULT (0),
    RowVersion             ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemMaster PRIMARY KEY CLUSTERED (ItemId),
    CONSTRAINT UQ_ItemMaster_Code UNIQUE (ItemCode),
    CONSTRAINT FK_ItemMaster_Category FOREIGN KEY (ItemCategoryId) REFERENCES dbo.ItemCategory (ItemCategoryId),
    CONSTRAINT FK_ItemMaster_Group    FOREIGN KEY (ItemGroupId)    REFERENCES dbo.ItemGroup (ItemGroupId),
    CONSTRAINT FK_ItemMaster_SubGroup FOREIGN KEY (ItemSubGroupId) REFERENCES dbo.ItemSubGroup (ItemSubGroupId),
    CONSTRAINT FK_ItemMaster_Family   FOREIGN KEY (ItemFamilyId)   REFERENCES dbo.ItemFamily (ItemFamilyId),
    CONSTRAINT FK_ItemMaster_Template FOREIGN KEY (AttributeTemplateId) REFERENCES dbo.AttributeTemplate (AttributeTemplateId),
    CONSTRAINT FK_ItemMaster_BaseUOM  FOREIGN KEY (BaseUOMId)      REFERENCES dbo.UOM (UOMId),
    CONSTRAINT CK_ItemMaster_Status
        CHECK (ItemStatus IN ('Draft','PendingApproval','Active','Inactive','Obsolete')),
    CONSTRAINT CK_ItemMaster_StockFlags CHECK (CanManufacture = 0 OR CanStock = 1),
    /* §12.1 rule 2 — 3–30 chars, uppercase, hyphen-delimited. */
    CONSTRAINT CK_ItemMaster_CodeFormat
        CHECK (LEN(ItemCode) BETWEEN 3 AND 30
               AND ItemCode COLLATE Latin1_General_BIN2 NOT LIKE '%[^A-Z0-9-]%'),
    /* §12.1 rule 3 — at least one of CanPurchase / CanSell / CanManufacture. */
    CONSTRAINT CK_ItemMaster_TransactableFlags
        CHECK (CanPurchase = 1 OR CanSell = 1 OR CanManufacture = 1),
    CONSTRAINT CK_ItemMaster_VersionNumber CHECK (VersionNumber >= 1)
);
GO

/* --------------------------------------------------------------------------
   §4.11 / §10.9.9 ItemAttribute
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemAttribute', N'U') IS NULL
CREATE TABLE dbo.ItemAttribute
(
    ItemAttributeId         BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId                  BIGINT                NOT NULL,
    AttributeDefinitionId   INT                   NOT NULL,
    ValueText               NVARCHAR(500)         NULL,
    ValueNumber             DECIMAL(18,4)         NULL,
    ValueDate               DATE                  NULL,
    ValueBoolean            BIT                   NULL,
    CreatedBy               INT                   NOT NULL,
    CreatedDate             DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy              INT                   NULL,
    ModifiedDate            DATETIME2(3)          NULL,
    IsActive                BIT                   NOT NULL DEFAULT (1),
    IsDeleted               BIT                   NOT NULL DEFAULT (0),
    RowVersion              ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemAttribute PRIMARY KEY CLUSTERED (ItemAttributeId),
    CONSTRAINT FK_ItemAttribute_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemAttribute_Definition FOREIGN KEY (AttributeDefinitionId)
        REFERENCES dbo.AttributeDefinition (AttributeDefinitionId),
    CONSTRAINT UQ_ItemAttribute_ItemDefinition UNIQUE (ItemId, AttributeDefinitionId),
    CONSTRAINT CK_ItemAttribute_SingleValue CHECK (
        (CASE WHEN ValueText    IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ValueNumber  IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ValueDate    IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ValueBoolean IS NOT NULL THEN 1 ELSE 0 END) = 1
    )
    /* The second half of the §4.11 CK — that the populated column matches the
       referenced AttributeDefinition.DataType — spans two tables and therefore
       cannot be a CHECK constraint. It is enforced by TR_ItemAttribute_ValueDataType
       in 10_triggers.sql (§12.1 rule 6). */
);
GO

/* --------------------------------------------------------------------------
   §5.3 BusinessUnitItem
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BusinessUnitItem', N'U') IS NULL
CREATE TABLE dbo.BusinessUnitItem
(
    BusinessUnitItemId  BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId              BIGINT                NOT NULL,
    BusinessUnitId      INT                   NOT NULL,
    IsAuthorized        BIT                   NOT NULL DEFAULT (1),
    LocalItemCode       VARCHAR(30)           NULL,
    AuthorizedDate      DATE                  NOT NULL,
    CreatedBy           INT                   NOT NULL,
    CreatedDate         DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                   NULL,
    ModifiedDate        DATETIME2(3)          NULL,
    IsActive            BIT                   NOT NULL DEFAULT (1),
    IsDeleted           BIT                   NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION            NOT NULL,
    CONSTRAINT PK_BusinessUnitItem PRIMARY KEY CLUSTERED (BusinessUnitItemId),
    CONSTRAINT FK_BusinessUnitItem_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_BusinessUnitItem_Unit FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    CONSTRAINT UQ_BusinessUnitItem_ItemUnit UNIQUE (ItemId, BusinessUnitId)
);
GO

/* --------------------------------------------------------------------------
   §5.6 ItemPackaging
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemPackaging', N'U') IS NULL
CREATE TABLE dbo.ItemPackaging
(
    ItemPackagingId     INT IDENTITY(1,1)  NOT NULL,
    ItemId              BIGINT             NOT NULL,
    PackagingLevel      TINYINT            NOT NULL,   -- 1 Each, 2 Inner, 3 Outer/Carton, 4 Pallet
    PackagingUOMId      INT                NOT NULL,
    QtyPerParentLevel   DECIMAL(18,4)      NOT NULL,
    IsPurchaseUOM       BIT                NOT NULL DEFAULT (0),
    IsSalesUOM          BIT                NOT NULL DEFAULT (0),
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemPackaging PRIMARY KEY CLUSTERED (ItemPackagingId),
    CONSTRAINT FK_ItemPackaging_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemPackaging_UOM  FOREIGN KEY (PackagingUOMId) REFERENCES dbo.UOM (UOMId),
    CONSTRAINT UQ_ItemPackaging_ItemLevel UNIQUE (ItemId, PackagingLevel),
    CONSTRAINT CK_ItemPackaging_Qty CHECK (QtyPerParentLevel > 0),
    CONSTRAINT CK_ItemPackaging_Level CHECK (PackagingLevel BETWEEN 1 AND 4)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '05_schema_item.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('05_schema_item.sql');
GO
