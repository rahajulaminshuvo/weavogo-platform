/* =============================================================================
   Universal Item Master — 06_schema_functional.sql
   Chapter 6 — Inventory, Procurement, Sales, Financial and Manufacturing mapping,
   plus the AssetMaster capitalization bridge.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §6.1.1 Warehouse
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Warehouse', N'U') IS NULL
CREATE TABLE dbo.Warehouse
(
    WarehouseId      INT IDENTITY(1,1)  NOT NULL,
    WarehouseCode    VARCHAR(10)        NOT NULL,
    WarehouseName    VARCHAR(100)       NOT NULL,
    BusinessUnitId   INT                NOT NULL,
    WarehouseType    VARCHAR(20)        NOT NULL,
    Address          VARCHAR(300)       NULL,
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_Warehouse PRIMARY KEY CLUSTERED (WarehouseId),
    CONSTRAINT FK_Warehouse_BusinessUnit FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    CONSTRAINT UQ_Warehouse_Code UNIQUE (WarehouseCode),
    CONSTRAINT CK_Warehouse_Type CHECK (WarehouseType IN ('Main','Transit','Quarantine','RawMaterial','FinishedGoods'))
);
GO

/* --------------------------------------------------------------------------
   §6.1.2 / §10.9.10 WarehouseItem
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.WarehouseItem', N'U') IS NULL
CREATE TABLE dbo.WarehouseItem
(
    WarehouseItemId    BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId             BIGINT                NOT NULL,
    WarehouseId        INT                   NOT NULL,
    QuantityOnHand     DECIMAL(18,4)         NOT NULL DEFAULT (0),
    QuantityReserved   DECIMAL(18,4)         NOT NULL DEFAULT (0),
    BinLocation        VARCHAR(30)           NULL,
    LastCountDate      DATE                  NULL,
    CreatedBy          INT                   NOT NULL,
    CreatedDate        DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy         INT                   NULL,
    ModifiedDate       DATETIME2(3)          NULL,
    IsActive           BIT                   NOT NULL DEFAULT (1),
    IsDeleted          BIT                   NOT NULL DEFAULT (0),
    RowVersion         ROWVERSION            NOT NULL,
    CONSTRAINT PK_WarehouseItem PRIMARY KEY CLUSTERED (WarehouseItemId),
    CONSTRAINT FK_WarehouseItem_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_WarehouseItem_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse (WarehouseId),
    CONSTRAINT UQ_WarehouseItem_ItemWarehouse UNIQUE (ItemId, WarehouseId)
);
GO

/* --------------------------------------------------------------------------
   §6.1.3 InventoryPolicy
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.InventoryPolicy', N'U') IS NULL
CREATE TABLE dbo.InventoryPolicy
(
    PolicyId         INT IDENTITY(1,1)  NOT NULL,
    ItemId           BIGINT             NOT NULL,
    BusinessUnitId   INT                NOT NULL,
    ReorderLevel     DECIMAL(18,4)      NOT NULL DEFAULT (0),
    SafetyStock      DECIMAL(18,4)      NOT NULL DEFAULT (0),
    MaxStock         DECIMAL(18,4)      NULL,
    ABCClass         CHAR(1)            NULL,
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_InventoryPolicy PRIMARY KEY CLUSTERED (PolicyId),
    CONSTRAINT FK_InventoryPolicy_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_InventoryPolicy_Unit FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    CONSTRAINT UQ_InventoryPolicy_ItemUnit UNIQUE (ItemId, BusinessUnitId),
    CONSTRAINT CK_InventoryPolicy_ABC CHECK (ABCClass IN ('A','B','C') OR ABCClass IS NULL)
);
GO

/* --------------------------------------------------------------------------
   §6.2.1 Supplier
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Supplier', N'U') IS NULL
CREATE TABLE dbo.Supplier
(
    SupplierId          INT IDENTITY(1,1)  NOT NULL,
    SupplierCode        VARCHAR(15)        NOT NULL,
    SupplierName        VARCHAR(150)       NOT NULL,
    Country             VARCHAR(60)        NOT NULL,
    PaymentTermsDays    INT                NOT NULL DEFAULT (30),
    TaxRegistrationNo   VARCHAR(30)        NULL,
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_Supplier PRIMARY KEY CLUSTERED (SupplierId),
    CONSTRAINT UQ_Supplier_Code UNIQUE (SupplierCode),
    CONSTRAINT CK_Supplier_PaymentTerms CHECK (PaymentTermsDays >= 0)
);
GO

/* --------------------------------------------------------------------------
   §6.2.2 SupplierItem
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.SupplierItem', N'U') IS NULL
CREATE TABLE dbo.SupplierItem
(
    SupplierItemId       BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId               BIGINT                NOT NULL,
    SupplierId           INT                   NOT NULL,
    SupplierPartNumber   VARCHAR(50)           NULL,
    PurchaseUOMId        INT                   NOT NULL,
    IsPreferred          BIT                   NOT NULL DEFAULT (0),
    LeadTimeDays         INT                   NOT NULL DEFAULT (0),
    MinOrderQty          DECIMAL(18,4)         NOT NULL DEFAULT (1),
    CreatedBy            INT                   NOT NULL,
    CreatedDate          DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy           INT                   NULL,
    ModifiedDate         DATETIME2(3)          NULL,
    IsActive             BIT                   NOT NULL DEFAULT (1),
    IsDeleted            BIT                   NOT NULL DEFAULT (0),
    RowVersion           ROWVERSION            NOT NULL,
    CONSTRAINT PK_SupplierItem PRIMARY KEY CLUSTERED (SupplierItemId),
    CONSTRAINT FK_SupplierItem_Item     FOREIGN KEY (ItemId)        REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_SupplierItem_Supplier FOREIGN KEY (SupplierId)    REFERENCES dbo.Supplier (SupplierId),
    CONSTRAINT FK_SupplierItem_UOM      FOREIGN KEY (PurchaseUOMId) REFERENCES dbo.UOM (UOMId),
    CONSTRAINT UQ_SupplierItem_ItemSupplier UNIQUE (ItemId, SupplierId),
    CONSTRAINT CK_SupplierItem_LeadTime CHECK (LeadTimeDays >= 0),
    CONSTRAINT CK_SupplierItem_MinOrderQty CHECK (MinOrderQty > 0)
);
GO

/* §12.1 rule 23 — at most one preferred supplier per item (filtered unique index). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_SupplierItem_OnePreferredPerItem'
               AND object_id = OBJECT_ID(N'dbo.SupplierItem'))
    CREATE UNIQUE INDEX UX_SupplierItem_OnePreferredPerItem
        ON dbo.SupplierItem (ItemId)
        WHERE IsPreferred = 1 AND IsDeleted = 0;
GO

/* --------------------------------------------------------------------------
   §6.2.3 SupplierPrice
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.SupplierPrice', N'U') IS NULL
CREATE TABLE dbo.SupplierPrice
(
    SupplierPriceId   BIGINT IDENTITY(1,1)  NOT NULL,
    SupplierItemId    BIGINT                NOT NULL,
    UnitPrice         DECIMAL(18,4)         NOT NULL,
    Currency          CHAR(3)               NOT NULL,
    EffectiveFrom     DATE                  NOT NULL,
    EffectiveTo       DATE                  NULL,
    CreatedBy         INT                   NOT NULL,
    CreatedDate       DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy        INT                   NULL,
    ModifiedDate      DATETIME2(3)          NULL,
    IsActive          BIT                   NOT NULL DEFAULT (1),
    IsDeleted         BIT                   NOT NULL DEFAULT (0),
    RowVersion        ROWVERSION            NOT NULL,
    CONSTRAINT PK_SupplierPrice PRIMARY KEY CLUSTERED (SupplierPriceId),
    CONSTRAINT FK_SupplierPrice_SupplierItem FOREIGN KEY (SupplierItemId) REFERENCES dbo.SupplierItem (SupplierItemId),
    CONSTRAINT CK_SupplierPrice_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT CK_SupplierPrice_Range CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
    /* Non-overlap of [EffectiveFrom, EffectiveTo] per SupplierItemId is enforced in the
       application layer — SQL Server has no range-exclusion constraint (§6.2.3). */
);
GO

/* --------------------------------------------------------------------------
   §6.3.1 Customer / CustomerItem / SalesPrice
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.Customer', N'U') IS NULL
CREATE TABLE dbo.Customer
(
    CustomerId          INT IDENTITY(1,1)  NOT NULL,
    CustomerCode        VARCHAR(15)        NOT NULL,
    CustomerName        VARCHAR(150)       NOT NULL,
    Country             VARCHAR(60)        NOT NULL,
    TaxRegistrationNo   VARCHAR(30)        NULL,
    CreatedBy           INT                NOT NULL,
    CreatedDate         DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy          INT                NULL,
    ModifiedDate        DATETIME2(3)       NULL,
    IsActive            BIT                NOT NULL DEFAULT (1),
    IsDeleted           BIT                NOT NULL DEFAULT (0),
    RowVersion          ROWVERSION         NOT NULL,
    CONSTRAINT PK_Customer PRIMARY KEY CLUSTERED (CustomerId),
    CONSTRAINT UQ_Customer_Code UNIQUE (CustomerCode)
);
GO

IF OBJECT_ID(N'dbo.CustomerItem', N'U') IS NULL
CREATE TABLE dbo.CustomerItem
(
    CustomerItemId     BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId             BIGINT                NOT NULL,
    CustomerId         INT                   NOT NULL,
    CustomerItemCode   VARCHAR(50)           NULL,
    IsAuthorized       BIT                   NOT NULL DEFAULT (1),
    CreatedBy          INT                   NOT NULL,
    CreatedDate        DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy         INT                   NULL,
    ModifiedDate       DATETIME2(3)          NULL,
    IsActive           BIT                   NOT NULL DEFAULT (1),
    IsDeleted          BIT                   NOT NULL DEFAULT (0),
    RowVersion         ROWVERSION            NOT NULL,
    CONSTRAINT PK_CustomerItem PRIMARY KEY CLUSTERED (CustomerItemId),
    CONSTRAINT FK_CustomerItem_Item     FOREIGN KEY (ItemId)     REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_CustomerItem_Customer FOREIGN KEY (CustomerId) REFERENCES dbo.Customer (CustomerId),
    CONSTRAINT UQ_CustomerItem_ItemCustomer UNIQUE (ItemId, CustomerId)
);
GO

/* SalesPrice mirrors SupplierPrice, keyed by CustomerItemId, with the addition that
   CustomerId may be NULL to represent a general list price (§6.3.1). Because a general
   list price has no CustomerItem parent, ItemId is carried directly and CustomerItemId
   is nullable — one row shape covers both list and customer-specific prices. */
IF OBJECT_ID(N'dbo.SalesPrice', N'U') IS NULL
CREATE TABLE dbo.SalesPrice
(
    SalesPriceId      BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId            BIGINT                NOT NULL,
    CustomerItemId    BIGINT                NULL,
    CustomerId        INT                   NULL,
    UnitPrice         DECIMAL(18,4)         NOT NULL,
    Currency          CHAR(3)               NOT NULL,
    EffectiveFrom     DATE                  NOT NULL,
    EffectiveTo       DATE                  NULL,
    CreatedBy         INT                   NOT NULL,
    CreatedDate       DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy        INT                   NULL,
    ModifiedDate      DATETIME2(3)          NULL,
    IsActive          BIT                   NOT NULL DEFAULT (1),
    IsDeleted         BIT                   NOT NULL DEFAULT (0),
    RowVersion        ROWVERSION            NOT NULL,
    CONSTRAINT PK_SalesPrice PRIMARY KEY CLUSTERED (SalesPriceId),
    CONSTRAINT FK_SalesPrice_Item         FOREIGN KEY (ItemId)         REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_SalesPrice_CustomerItem FOREIGN KEY (CustomerItemId) REFERENCES dbo.CustomerItem (CustomerItemId),
    CONSTRAINT FK_SalesPrice_Customer     FOREIGN KEY (CustomerId)     REFERENCES dbo.Customer (CustomerId),
    CONSTRAINT CK_SalesPrice_UnitPrice CHECK (UnitPrice >= 0),
    CONSTRAINT CK_SalesPrice_Range CHECK (EffectiveTo IS NULL OR EffectiveTo >= EffectiveFrom)
);
GO

/* --------------------------------------------------------------------------
   §6.4.1 GLMapping
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.GLMapping', N'U') IS NULL
CREATE TABLE dbo.GLMapping
(
    GLMappingId       INT IDENTITY(1,1)  NOT NULL,
    ItemCategoryId    INT                NULL,
    ItemId            BIGINT             NULL,
    BusinessUnitId    INT                NOT NULL,
    InventoryGLCode   VARCHAR(20)        NOT NULL,
    COGSGLCode        VARCHAR(20)        NOT NULL,
    SalesGLCode       VARCHAR(20)        NULL,
    PurchaseGLCode    VARCHAR(20)        NOT NULL,
    CreatedBy         INT                NOT NULL,
    CreatedDate       DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy        INT                NULL,
    ModifiedDate      DATETIME2(3)       NULL,
    IsActive          BIT                NOT NULL DEFAULT (1),
    IsDeleted         BIT                NOT NULL DEFAULT (0),
    RowVersion        ROWVERSION         NOT NULL,
    CONSTRAINT PK_GLMapping PRIMARY KEY CLUSTERED (GLMappingId),
    CONSTRAINT FK_GLMapping_Category FOREIGN KEY (ItemCategoryId) REFERENCES dbo.ItemCategory (ItemCategoryId),
    CONSTRAINT FK_GLMapping_Item     FOREIGN KEY (ItemId)         REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_GLMapping_Unit     FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    /* §12.1 rule 15 — exactly one of ItemCategoryId, ItemId is non-null. */
    CONSTRAINT CK_GLMapping_OneTarget CHECK (
        (CASE WHEN ItemCategoryId IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ItemId         IS NOT NULL THEN 1 ELSE 0 END) = 1
    )
);
GO

/* --------------------------------------------------------------------------
   §6.4.2 TaxMapping
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.TaxMapping', N'U') IS NULL
CREATE TABLE dbo.TaxMapping
(
    TaxMappingId     INT IDENTITY(1,1)  NOT NULL,
    ItemCategoryId   INT                NULL,
    ItemId           BIGINT             NULL,
    Jurisdiction     VARCHAR(50)        NOT NULL,
    TaxCode          VARCHAR(20)        NOT NULL,
    TaxRatePercent   DECIMAL(5,2)       NOT NULL,
    TaxType          VARCHAR(20)        NOT NULL,
    CreatedBy        INT                NOT NULL,
    CreatedDate      DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                NULL,
    ModifiedDate     DATETIME2(3)       NULL,
    IsActive         BIT                NOT NULL DEFAULT (1),
    IsDeleted        BIT                NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION         NOT NULL,
    CONSTRAINT PK_TaxMapping PRIMARY KEY CLUSTERED (TaxMappingId),
    CONSTRAINT FK_TaxMapping_Category FOREIGN KEY (ItemCategoryId) REFERENCES dbo.ItemCategory (ItemCategoryId),
    CONSTRAINT FK_TaxMapping_Item     FOREIGN KEY (ItemId)         REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT CK_TaxMapping_OneTarget CHECK (
        (CASE WHEN ItemCategoryId IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ItemId         IS NOT NULL THEN 1 ELSE 0 END) = 1
    ),
    CONSTRAINT CK_TaxMapping_Type CHECK (TaxType IN ('VAT','GST','SalesTax','Exempt','WithholdingTax')),
    CONSTRAINT CK_TaxMapping_Rate CHECK (TaxRatePercent >= 0)
);
GO

/* §12.1 rule 16 — unique per (target, Jurisdiction); two filtered indexes because
   the target is either a category or an item, never both. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TaxMapping_CategoryJurisdiction'
               AND object_id = OBJECT_ID(N'dbo.TaxMapping'))
    CREATE UNIQUE INDEX UX_TaxMapping_CategoryJurisdiction
        ON dbo.TaxMapping (ItemCategoryId, Jurisdiction)
        WHERE ItemCategoryId IS NOT NULL AND IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_TaxMapping_ItemJurisdiction'
               AND object_id = OBJECT_ID(N'dbo.TaxMapping'))
    CREATE UNIQUE INDEX UX_TaxMapping_ItemJurisdiction
        ON dbo.TaxMapping (ItemId, Jurisdiction)
        WHERE ItemId IS NOT NULL AND IsDeleted = 0;
GO

/* --------------------------------------------------------------------------
   §6.5.1 BillOfMaterial
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BillOfMaterial', N'U') IS NULL
CREATE TABLE dbo.BillOfMaterial
(
    BOMId           INT IDENTITY(1,1)  NOT NULL,
    ParentItemId    BIGINT             NOT NULL,
    BOMVersion      INT                NOT NULL DEFAULT (1),
    EffectiveDate   DATE               NOT NULL,
    Status          VARCHAR(20)        NOT NULL DEFAULT ('Draft'),
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_BillOfMaterial PRIMARY KEY CLUSTERED (BOMId),
    CONSTRAINT FK_BillOfMaterial_ParentItem FOREIGN KEY (ParentItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT UQ_BOM_ParentVersion UNIQUE (ParentItemId, BOMVersion),
    CONSTRAINT CK_BOM_Status CHECK (Status IN ('Draft','Active','Obsolete')),
    CONSTRAINT CK_BOM_Version CHECK (BOMVersion >= 1)
);
GO

/* §12.1 rule 17 — at most one Active BOMVersion per ParentItemId. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BOM_OneActivePerParent'
               AND object_id = OBJECT_ID(N'dbo.BillOfMaterial'))
    CREATE UNIQUE INDEX UX_BOM_OneActivePerParent
        ON dbo.BillOfMaterial (ParentItemId)
        WHERE Status = 'Active' AND IsDeleted = 0;
GO

/* --------------------------------------------------------------------------
   §6.5.2 BOMComponent
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.BOMComponent', N'U') IS NULL
CREATE TABLE dbo.BOMComponent
(
    BOMComponentId    BIGINT IDENTITY(1,1)  NOT NULL,
    BOMId             INT                   NOT NULL,
    ComponentItemId   BIGINT                NOT NULL,
    QtyPerUnit        DECIMAL(18,6)         NOT NULL,
    UOMId             INT                   NOT NULL,
    ScrapPercent      DECIMAL(5,2)          NOT NULL DEFAULT (0),
    CreatedBy         INT                   NOT NULL,
    CreatedDate       DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy        INT                   NULL,
    ModifiedDate      DATETIME2(3)          NULL,
    IsActive          BIT                   NOT NULL DEFAULT (1),
    IsDeleted         BIT                   NOT NULL DEFAULT (0),
    RowVersion        ROWVERSION            NOT NULL,
    CONSTRAINT PK_BOMComponent PRIMARY KEY CLUSTERED (BOMComponentId),
    CONSTRAINT FK_BOMComponent_BOM  FOREIGN KEY (BOMId)           REFERENCES dbo.BillOfMaterial (BOMId),
    CONSTRAINT FK_BOMComponent_Item FOREIGN KEY (ComponentItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_BOMComponent_UOM  FOREIGN KEY (UOMId)           REFERENCES dbo.UOM (UOMId),
    CONSTRAINT UQ_BOMComponent_BOMItem UNIQUE (BOMId, ComponentItemId),
    CONSTRAINT CK_BOMComponent_Qty CHECK (QtyPerUnit >= 0),
    CONSTRAINT CK_BOMComponent_Scrap CHECK (ScrapPercent >= 0 AND ScrapPercent < 100)
    /* Self-reference and cycle prevention (§12.1 rule 18) require traversing the BOM
       graph and are enforced by TR_BOMComponent_CircularCheck in 10_triggers.sql. */
);
GO

/* --------------------------------------------------------------------------
   §6.6.1 AssetMaster
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.AssetMaster', N'U') IS NULL
CREATE TABLE dbo.AssetMaster
(
    AssetId                   INT IDENTITY(1,1)  NOT NULL,
    ItemId                    BIGINT             NOT NULL,
    AssetCode                 VARCHAR(20)        NOT NULL,
    CapitalizationDate        DATE               NOT NULL,
    AcquisitionCost           DECIMAL(18,2)      NOT NULL,
    DepreciationMethod        VARCHAR(20)        NOT NULL,
    UsefulLifeMonths          INT                NOT NULL,
    AccumulatedDepreciation   DECIMAL(18,2)      NOT NULL DEFAULT (0),
    AssetStatus               VARCHAR(20)        NOT NULL DEFAULT ('InUse'),
    BusinessUnitId            INT                NOT NULL,
    CreatedBy                 INT                NOT NULL,
    CreatedDate               DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy                INT                NULL,
    ModifiedDate              DATETIME2(3)       NULL,
    IsActive                  BIT                NOT NULL DEFAULT (1),
    IsDeleted                 BIT                NOT NULL DEFAULT (0),
    RowVersion                ROWVERSION         NOT NULL,
    CONSTRAINT PK_AssetMaster PRIMARY KEY CLUSTERED (AssetId),
    CONSTRAINT FK_AssetMaster_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_AssetMaster_Unit FOREIGN KEY (BusinessUnitId) REFERENCES dbo.BusinessUnit (BusinessUnitId),
    CONSTRAINT UQ_AssetMaster_Code UNIQUE (AssetCode),
    CONSTRAINT CK_AssetMaster_Method CHECK (DepreciationMethod IN ('StraightLine','DecliningBalance','UnitsOfProduction')),
    CONSTRAINT CK_AssetMaster_Status CHECK (AssetStatus IN ('InUse','UnderMaintenance','Disposed')),
    CONSTRAINT CK_AssetMaster_UsefulLife CHECK (UsefulLifeMonths > 0),
    CONSTRAINT CK_AssetMaster_Cost CHECK (AcquisitionCost >= 0)
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '06_schema_functional.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('06_schema_functional.sql');
GO
