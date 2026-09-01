/* =============================================================================
   Universal Item Master — 07_schema_quality.sql
   Chapter 7 — QC templates/parameters/profiles, lot & serial traceability,
   barcode & RFID identification, document attachment.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   §7.1.1 QCParameterTemplate
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.QCParameterTemplate', N'U') IS NULL
CREATE TABLE dbo.QCParameterTemplate
(
    QCTemplateId   INT IDENTITY(1,1)  NOT NULL,
    TemplateCode   VARCHAR(30)        NOT NULL,
    TemplateName   VARCHAR(100)       NOT NULL,
    Description    VARCHAR(500)       NULL,
    CreatedBy      INT                NOT NULL,
    CreatedDate    DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy     INT                NULL,
    ModifiedDate   DATETIME2(3)       NULL,
    IsActive       BIT                NOT NULL DEFAULT (1),
    IsDeleted      BIT                NOT NULL DEFAULT (0),
    RowVersion     ROWVERSION         NOT NULL,
    CONSTRAINT PK_QCParameterTemplate PRIMARY KEY CLUSTERED (QCTemplateId),
    CONSTRAINT UQ_QCParameterTemplate_Code UNIQUE (TemplateCode)
);
GO

/* --------------------------------------------------------------------------
   §7.1.2 QCParameter
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.QCParameter', N'U') IS NULL
CREATE TABLE dbo.QCParameter
(
    QCParameterId   INT IDENTITY(1,1)  NOT NULL,
    QCTemplateId    INT                NOT NULL,
    ParameterName   VARCHAR(100)       NOT NULL,
    DataType        VARCHAR(20)        NOT NULL,
    MinValue        DECIMAL(18,4)      NULL,
    MaxValue        DECIMAL(18,4)      NULL,
    UnitOfMeasure   VARCHAR(20)        NULL,
    TestMethod      VARCHAR(150)       NULL,
    IsCritical      BIT                NOT NULL DEFAULT (1),
    CreatedBy       INT                NOT NULL,
    CreatedDate     DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy      INT                NULL,
    ModifiedDate    DATETIME2(3)       NULL,
    IsActive        BIT                NOT NULL DEFAULT (1),
    IsDeleted       BIT                NOT NULL DEFAULT (0),
    RowVersion      ROWVERSION         NOT NULL,
    CONSTRAINT PK_QCParameter PRIMARY KEY CLUSTERED (QCParameterId),
    CONSTRAINT FK_QCParameter_Template FOREIGN KEY (QCTemplateId) REFERENCES dbo.QCParameterTemplate (QCTemplateId),
    CONSTRAINT UQ_QCParameter_TemplateName UNIQUE (QCTemplateId, ParameterName),
    CONSTRAINT CK_QCParameter_DataType CHECK (DataType IN ('Number','Text','Boolean')),
    CONSTRAINT CK_QCParameter_Range CHECK (MinValue IS NULL OR MaxValue IS NULL OR MinValue <= MaxValue)
);
GO

/* --------------------------------------------------------------------------
   §7.1.3 ItemQCProfile
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemQCProfile', N'U') IS NULL
CREATE TABLE dbo.ItemQCProfile
(
    ItemQCProfileId       INT IDENTITY(1,1)  NOT NULL,
    ItemFamilyId          INT                NULL,
    ItemId                BIGINT             NULL,
    QCTemplateId          INT                NOT NULL,
    IsMandatory           BIT                NOT NULL DEFAULT (1),
    InspectionFrequency   VARCHAR(20)        NOT NULL DEFAULT ('EveryBatch'),
    CreatedBy             INT                NOT NULL,
    CreatedDate           DATETIME2(3)       NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy            INT                NULL,
    ModifiedDate          DATETIME2(3)       NULL,
    IsActive              BIT                NOT NULL DEFAULT (1),
    IsDeleted             BIT                NOT NULL DEFAULT (0),
    RowVersion            ROWVERSION         NOT NULL,
    CONSTRAINT PK_ItemQCProfile PRIMARY KEY CLUSTERED (ItemQCProfileId),
    CONSTRAINT FK_ItemQCProfile_Family   FOREIGN KEY (ItemFamilyId) REFERENCES dbo.ItemFamily (ItemFamilyId),
    CONSTRAINT FK_ItemQCProfile_Item     FOREIGN KEY (ItemId)       REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemQCProfile_Template FOREIGN KEY (QCTemplateId) REFERENCES dbo.QCParameterTemplate (QCTemplateId),
    CONSTRAINT CK_ItemQCProfile_OneTarget CHECK (
        (CASE WHEN ItemFamilyId IS NOT NULL THEN 1 ELSE 0 END) +
        (CASE WHEN ItemId       IS NOT NULL THEN 1 ELSE 0 END) = 1
    ),
    CONSTRAINT CK_ItemQCProfile_Frequency CHECK (InspectionFrequency IN ('EveryBatch','Sampling','Annual'))
);
GO

/* --------------------------------------------------------------------------
   §7.2.1 ItemLot
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemLot', N'U') IS NULL
CREATE TABLE dbo.ItemLot
(
    ItemLotId          BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId             BIGINT                NOT NULL,
    LotNumber          VARCHAR(30)           NOT NULL,
    ManufactureDate    DATE                  NULL,
    ExpiryDate         DATE                  NULL,
    WarehouseId        INT                   NOT NULL,
    QuantityReceived   DECIMAL(18,4)         NOT NULL,
    QCStatus           VARCHAR(20)           NOT NULL DEFAULT ('Pending'),
    CreatedBy          INT                   NOT NULL,
    CreatedDate        DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy         INT                   NULL,
    ModifiedDate       DATETIME2(3)          NULL,
    IsActive           BIT                   NOT NULL DEFAULT (1),
    IsDeleted          BIT                   NOT NULL DEFAULT (0),
    RowVersion         ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemLot PRIMARY KEY CLUSTERED (ItemLotId),
    CONSTRAINT FK_ItemLot_Item      FOREIGN KEY (ItemId)      REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemLot_Warehouse FOREIGN KEY (WarehouseId) REFERENCES dbo.Warehouse (WarehouseId),
    CONSTRAINT UQ_ItemLot_ItemLotNumber UNIQUE (ItemId, LotNumber),
    CONSTRAINT CK_ItemLot_QCStatus CHECK (QCStatus IN ('Pending','Passed','Failed','Quarantined')),
    CONSTRAINT CK_ItemLot_Quantity CHECK (QuantityReceived > 0),
    CONSTRAINT CK_ItemLot_Dates CHECK (ExpiryDate IS NULL OR ManufactureDate IS NULL OR ExpiryDate >= ManufactureDate)
);
GO

/* --------------------------------------------------------------------------
   §7.2.2 ItemSerial
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemSerial', N'U') IS NULL
CREATE TABLE dbo.ItemSerial
(
    ItemSerialId             BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId                   BIGINT                NOT NULL,
    SerialNumber             VARCHAR(50)           NOT NULL,
    WarehouseId              INT                   NULL,
    Status                   VARCHAR(20)           NOT NULL DEFAULT ('InStock'),
    WarrantyExpiryDate       DATE                  NULL,
    LastCalibrationDate      DATE                  NULL,
    NextCalibrationDueDate   DATE                  NULL,
    LinkedAssetId            INT                   NULL,
    CreatedBy                INT                   NOT NULL,
    CreatedDate              DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy               INT                   NULL,
    ModifiedDate             DATETIME2(3)          NULL,
    IsActive                 BIT                   NOT NULL DEFAULT (1),
    IsDeleted                BIT                   NOT NULL DEFAULT (0),
    RowVersion               ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemSerial PRIMARY KEY CLUSTERED (ItemSerialId),
    CONSTRAINT FK_ItemSerial_Item      FOREIGN KEY (ItemId)        REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemSerial_Warehouse FOREIGN KEY (WarehouseId)   REFERENCES dbo.Warehouse (WarehouseId),
    CONSTRAINT FK_ItemSerial_Asset     FOREIGN KEY (LinkedAssetId) REFERENCES dbo.AssetMaster (AssetId),
    CONSTRAINT UQ_ItemSerial_ItemSerialNumber UNIQUE (ItemId, SerialNumber),
    CONSTRAINT CK_ItemSerial_Status CHECK (Status IN ('InStock','Issued','UnderRepair','Retired')),
    CONSTRAINT CK_ItemSerial_Calibration
        CHECK (NextCalibrationDueDate IS NULL OR LastCalibrationDate IS NULL
               OR NextCalibrationDueDate >= LastCalibrationDate)
);
GO

/* --------------------------------------------------------------------------
   §7.3.1 ItemBarcode
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemBarcode', N'U') IS NULL
CREATE TABLE dbo.ItemBarcode
(
    ItemBarcodeId     BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId            BIGINT                NOT NULL,
    ItemPackagingId   INT                   NULL,
    BarcodeType       VARCHAR(20)           NOT NULL,
    BarcodeValue      VARCHAR(50)           NOT NULL,
    IsPrimary         BIT                   NOT NULL DEFAULT (1),
    CreatedBy         INT                   NOT NULL,
    CreatedDate       DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy        INT                   NULL,
    ModifiedDate      DATETIME2(3)          NULL,
    IsActive          BIT                   NOT NULL DEFAULT (1),
    IsDeleted         BIT                   NOT NULL DEFAULT (0),
    RowVersion        ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemBarcode PRIMARY KEY CLUSTERED (ItemBarcodeId),
    CONSTRAINT FK_ItemBarcode_Item      FOREIGN KEY (ItemId)          REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemBarcode_Packaging FOREIGN KEY (ItemPackagingId) REFERENCES dbo.ItemPackaging (ItemPackagingId),
    CONSTRAINT UQ_ItemBarcode_Value UNIQUE (BarcodeValue),
    CONSTRAINT CK_ItemBarcode_Type CHECK (BarcodeType IN ('EAN13','UPC-A','Code128','QR'))
);
GO

/* --------------------------------------------------------------------------
   §7.3.2 ItemRFID
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemRFID', N'U') IS NULL
CREATE TABLE dbo.ItemRFID
(
    ItemRFIDId     BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId         BIGINT                NOT NULL,
    ItemSerialId   BIGINT                NULL,
    EPCCode        VARCHAR(50)           NOT NULL,
    TagType        VARCHAR(20)           NOT NULL,
    AssignedDate   DATE                  NOT NULL,
    CreatedBy      INT                   NOT NULL,
    CreatedDate    DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy     INT                   NULL,
    ModifiedDate   DATETIME2(3)          NULL,
    IsActive       BIT                   NOT NULL DEFAULT (1),
    IsDeleted      BIT                   NOT NULL DEFAULT (0),
    RowVersion     ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemRFID PRIMARY KEY CLUSTERED (ItemRFIDId),
    CONSTRAINT FK_ItemRFID_Item   FOREIGN KEY (ItemId)       REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT FK_ItemRFID_Serial FOREIGN KEY (ItemSerialId) REFERENCES dbo.ItemSerial (ItemSerialId),
    CONSTRAINT UQ_ItemRFID_EPCCode UNIQUE (EPCCode),
    CONSTRAINT CK_ItemRFID_TagType CHECK (TagType IN ('Passive','Active'))
);
GO

/* --------------------------------------------------------------------------
   §7.4.1 ItemDocument
   -------------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ItemDocument', N'U') IS NULL
CREATE TABLE dbo.ItemDocument
(
    ItemDocumentId   BIGINT IDENTITY(1,1)  NOT NULL,
    ItemId           BIGINT                NOT NULL,
    DocumentType     VARCHAR(30)           NOT NULL,
    FileName         VARCHAR(255)          NOT NULL,
    FileURL          VARCHAR(500)          NOT NULL,
    Version          VARCHAR(10)           NOT NULL DEFAULT ('1.0'),
    UploadedDate     DATE                  NOT NULL,
    CreatedBy        INT                   NOT NULL,
    CreatedDate      DATETIME2(3)          NOT NULL DEFAULT (SYSUTCDATETIME()),
    ModifiedBy       INT                   NULL,
    ModifiedDate     DATETIME2(3)          NULL,
    IsActive         BIT                   NOT NULL DEFAULT (1),
    IsDeleted        BIT                   NOT NULL DEFAULT (0),
    RowVersion       ROWVERSION            NOT NULL,
    CONSTRAINT PK_ItemDocument PRIMARY KEY CLUSTERED (ItemDocumentId),
    CONSTRAINT FK_ItemDocument_Item FOREIGN KEY (ItemId) REFERENCES dbo.ItemMaster (ItemId),
    CONSTRAINT UQ_ItemDocument_ItemTypeVersion UNIQUE (ItemId, DocumentType, Version),
    CONSTRAINT CK_ItemDocument_Type
        CHECK (DocumentType IN ('Datasheet','MSDS','ComplianceCertificate','Image','UserManual','Warranty'))
);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '07_schema_quality.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('07_schema_quality.sql');
GO
