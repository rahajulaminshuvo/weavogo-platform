/* =============================================================================
   Universal Item Master — 11_seed_reference.sql
   Reference (master) data, seeded with explicit identity values so that every Id
   matches the sample-data tables in the SDS and the worked FK examples line up.

   Sources: §3.9, §4.3–4.9, §5.2–5.5, §6.1.1, §6.2.1, §7.1.1–7.1.2, §8.2.1–8.2.2,
   §12.3. Re-runnable: every block is guarded by an existence check.

   TEST CREDENTIALS: all seeded users share the password  Weavo#2026
   (BCrypt hash below). Change them before any non-local deployment.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

DECLARE @pwd VARCHAR(255) = '$2a$11$.C43mZTqMjb9y2ghL53WJuwFTtlLcKSgTh1AEdXvUyOzr5TwWFnJa'; -- BCrypt('Weavo#2026', cost 11)

/* --------------------------------------------------------------------------
   UserAccount — §4.1 audit FK target, §12.3 security matrix actors
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.UserAccount)
BEGIN
    SET IDENTITY_INSERT dbo.UserAccount ON;
    INSERT dbo.UserAccount (UserId, UserName, FullName, Email, PasswordHash, IsSystemAdmin, CreatedBy)
    VALUES
        (1,  'system',    N'System Administrator',  'system@weavogo.local',    @pwd, 1, 1),
        (101,'catmgr',    N'Category Manager (ICT)','catmgr@weavogo.local',    @pwd, 0, 1),
        (102,'finctrl',   N'Finance Controller',    'finctrl@weavogo.local',   @pwd, 0, 1),
        (103,'qcmgr',     N'QC Manager',            'qcmgr@weavogo.local',     @pwd, 0, 1),
        (104,'whclerk',   N'Warehouse Clerk',       'whclerk@weavogo.local',   @pwd, 0, 1),
        (105,'salesuser', N'Sales User',            'salesuser@weavogo.local', @pwd, 0, 1),
        (106,'readonly',  N'Read Only User',        'readonly@weavogo.local',  @pwd, 0, 1),
        (107,'itsec',     N'IT Security Reviewer',  'itsec@weavogo.local',     @pwd, 0, 1);
    SET IDENTITY_INSERT dbo.UserAccount OFF;
END
GO

/* --------------------------------------------------------------------------
   Role — §12.3 plus the IT Security Review step role from §8.2.2
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Role)
BEGIN
    SET IDENTITY_INSERT dbo.Role ON;
    INSERT dbo.Role (RoleId, RoleCode, RoleName, Description, IsScoped, CreatedBy)
    VALUES
        (1, 'SYSADMIN',   'System Administrator', 'Every permission, unscoped; plus user and role management (§12.3 note).', 0, 1),
        (2, 'CATMGR',     'Category Manager',     'Creates, edits, submits and category-approves items within their business units.', 1, 1),
        (3, 'FINCTRL',    'Finance Controller',   'Finance approval step; sees SupplierPrice and GLMapping.', 0, 1),
        (4, 'QCMGR',      'QC Manager',           'QC approval step; posts lot QC results.', 0, 1),
        (5, 'WHCLERK',    'Warehouse Clerk',      'Records WarehouseItem transactions within their own unit.', 1, 1),
        (6, 'SALESUSER',  'Sales User',           'Views items authorized for their own unit.', 1, 1),
        (7, 'READONLY',   'Read-Only',            'View item detail only.', 0, 1),
        (8, 'ITSECURITY', 'IT Security Review',   'Second approval step of the ICT-ASSET workflow (§8.2.2).', 0, 1);
    SET IDENTITY_INSERT dbo.Role OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.UserRole)
    INSERT dbo.UserRole (UserId, RoleId, CreatedBy)
    VALUES (1,1,1), (101,2,1), (102,3,1), (103,4,1), (104,5,1), (105,6,1), (106,7,1), (107,8,1);
GO

/* --------------------------------------------------------------------------
   §5.2.1 Company
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Company)
BEGIN
    SET IDENTITY_INSERT dbo.Company ON;
    INSERT dbo.Company (CompanyId, CompanyCode, CompanyName, Country, CreatedBy)
    VALUES
        (1, 'WEAVOGO', 'WeavoGo Group Ltd.',         'Bangladesh', 1),
        (2, 'TISWL',   'That''s It Sports Ware Ltd.','Bangladesh', 1);
    SET IDENTITY_INSERT dbo.Company OFF;
END
GO

/* --------------------------------------------------------------------------
   §5.2.2 / §5.2.3 BusinessUnit — 13 WeavoGo units + 6 TISWL units
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BusinessUnit)
BEGIN
    SET IDENTITY_INSERT dbo.BusinessUnit ON;
    INSERT dbo.BusinessUnit (BusinessUnitId, CompanyId, UnitCode, UnitName, BusinessType, CreatedBy)
    VALUES
        (1,  1, 'GAR',        'Garments Division',       'Manufacturing',       1),
        (2,  1, 'TXT',        'Textile Division',        'Manufacturing',       1),
        (3,  1, 'EMB',        'Embroidery Division',     'Manufacturing',       1),
        (4,  1, 'PRN',        'Printing Division',       'Manufacturing',       1),
        (5,  1, 'ACC',        'Accessories Division',    'Manufacturing',       1),
        (6,  1, 'ICT',        'ICT & Software Division', 'Information Technology', 1),
        (7,  1, 'CON',        'Construction Division',   'Construction',        1),
        (8,  1, 'TRN',        'Transportation Division', 'Logistics',           1),
        (9,  1, 'TEA',        'Tea Estate',              'Agriculture',         1),
        (10, 1, 'COR',        'Corporate Services',      'Administration',      1),
        (11, 1, 'DYE',        'Dyeing Factory',          'Manufacturing',       1),
        (12, 1, 'WSH',        'Washing Plant',           'Manufacturing',       1),
        (13, 1, 'HOS',        'Hospital',                'Healthcare',          1),
        (14, 2, 'TISWL-U1',   'TISWL Unit 1',            'Garments Production', 1),
        (15, 2, 'TISWL-U2',   'TISWL Unit 2',            'Garments Production', 1),
        (16, 2, 'TISWL-U3',   'TISWL Unit 3',            'Garments Production', 1),
        (17, 2, 'TISWL-U4',   'TISWL Unit 4',            'Garments Production', 1),
        (18, 2, 'TISWL-U4B',  'TISWL Unit 4B',           'Garments Production', 1),
        (19, 2, 'TISWL-EMB1', 'TISWL Emb 1',             'Embroidery',          1);
    SET IDENTITY_INSERT dbo.BusinessUnit OFF;
END
GO

/* Scope assignments for the test users (§12.3 "scoped"). */
IF NOT EXISTS (SELECT 1 FROM dbo.UserBusinessUnit)
    INSERT dbo.UserBusinessUnit (UserId, BusinessUnitId, IsPrimary, CreatedBy)
    VALUES
        (101, 6,  1, 1), (101, 10, 0, 1), (101, 1, 0, 1), (101, 7, 0, 1), (101, 13, 0, 1),
        (104, 6,  1, 1),
        (105, 1,  1, 1);
GO

/* --------------------------------------------------------------------------
   §5.4 UOM
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.UOM)
BEGIN
    SET IDENTITY_INSERT dbo.UOM ON;
    INSERT dbo.UOM (UOMId, UOMCode, UOMName, UOMType, DecimalPrecision, CreatedBy)
    VALUES
        (1,  'PCS',  'Piece',    'Count',  0, 1),
        (2,  'KG',   'Kilogram', 'Weight', 2, 1),
        (3,  'MTR',  'Meter',    'Length', 2, 1),
        (4,  'LTR',  'Liter',    'Volume', 2, 1),
        (5,  'DOZ',  'Dozen',    'Count',  0, 1),
        (6,  'BOX',  'Box',      'Count',  0, 1),
        (7,  'CTN',  'Carton',   'Count',  0, 1),
        (8,  'BAG',  'Bag',      'Weight', 0, 1),
        (9,  'ROLL', 'Roll',     'Length', 0, 1),
        (10, 'PLT',  'Pallet',   'Count',  0, 1);
    SET IDENTITY_INSERT dbo.UOM OFF;
END
GO

/* §5.5 UOMConversion — generic defaults; item-level packaging overrides them (§5.6). */
IF NOT EXISTS (SELECT 1 FROM dbo.UOMConversion)
BEGIN
    SET IDENTITY_INSERT dbo.UOMConversion ON;
    INSERT dbo.UOMConversion (UOMConversionId, FromUOMId, ToUOMId, ConversionFactor, CreatedBy)
    VALUES
        (1, 5, 1, 12.000000, 1),   -- DOZ -> PCS  : 1 dozen = 12 pieces
        (2, 6, 1, 1.000000,  1),   -- BOX -> PCS  : generic default, overridden per item
        (3, 2, 8, 0.020000,  1),   -- KG  -> BAG  : 1 bag = 50 KG
        (4, 3, 9, 0.010000,  1);   -- MTR -> ROLL : 1 roll = 100 MTR (fabric default)
    SET IDENTITY_INSERT dbo.UOMConversion OFF;
END
GO

/* --------------------------------------------------------------------------
   §4.7 AttributeDefinition — Ids 1–15 are fixed by the SDS (§4.7 excerpt and the
   §4.9 Laptop template); 16–38 complete the dictionary behind the §3.9 templates.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.AttributeDefinition)
BEGIN
    SET IDENTITY_INSERT dbo.AttributeDefinition ON;
    INSERT dbo.AttributeDefinition (AttributeDefinitionId, AttributeCode, AttributeName, DataType, UnitOfMeasure, EnumOptions, CreatedBy)
    VALUES
        (1,  'BRAND',        'Brand',              'Text',    NULL,      NULL, 1),
        (2,  'MODEL',        'Model',              'Text',    NULL,      NULL, 1),
        (3,  'GSM',          'GSM',                'Number',  'gsm',     NULL, 1),
        (4,  'COMPOSITION',  'Composition',        'Text',    NULL,      NULL, 1),
        (5,  'WIDTH',        'Width',              'Number',  'inch',    NULL, 1),
        (6,  'WARRANTY',     'Warranty',           'Number',  'years',   NULL, 1),
        (7,  'RAM',          'RAM',                'Number',  'GB',      NULL, 1),
        (8,  'CPU',          'CPU',                'Text',    NULL,      NULL, 1),
        (9,  'GRADE',        'Grade',              'Text',    NULL,      NULL, 1),
        (10, 'RESOLUTION',   'Resolution',         'Text',    NULL,      NULL, 1),
        (11, 'SSD',          'SSD',                'Number',  'TB',      NULL, 1),
        (12, 'DISPLAY',      'Display',            'Text',    NULL,      NULL, 1),
        (13, 'OS',           'Operating System',   'Text',    NULL,      NULL, 1),
        (14, 'SERIALNO',     'Serial No.',         'Text',    NULL,      NULL, 1),
        (15, 'ASSETTAG',     'Asset Tag',          'Text',    NULL,      NULL, 1),
        (16, 'SHRINKAGE',    'Shrinkage',          'Number',  '%',       NULL, 1),
        (17, 'FINISH',       'Finish',             'Text',    NULL,      NULL, 1),
        (18, 'PROCESSOR',    'Processor',          'Text',    NULL,      NULL, 1),
        (19, 'STORAGE',      'Storage',            'Text',    NULL,      NULL, 1),
        (20, 'RAID',         'RAID',               'Text',    NULL,      NULL, 1),
        (21, 'POWERSUPPLY',  'Power Supply',       'Text',    NULL,      NULL, 1),
        (22, 'RACKUNIT',     'Rack Unit',          'Enum',    NULL,      N'["1U","2U","4U","Tower"]', 1),
        (23, 'PORTS',        'Ports',              'Number',  'count',   NULL, 1),
        (24, 'THROUGHPUT',   'Throughput',         'Text',    NULL,      NULL, 1),
        (25, 'RACKMOUNT',    'Rack Mount',         'Boolean', NULL,      NULL, 1),
        (26, 'POWER',        'Power',              'Text',    NULL,      NULL, 1),
        (27, 'WEIGHT',       'Weight',             'Number',  'KG',      NULL, 1),
        (28, 'STANDARD',     'Standard',           'Text',    NULL,      NULL, 1),
        (29, 'MFGDATE',      'Manufacture Date',   'Date',    NULL,      NULL, 1),
        (30, 'SHELFLIFE',    'Shelf Life',         'Number',  'days',    NULL, 1),
        (31, 'DIAMETER',     'Diameter',           'Number',  'mm',      NULL, 1),
        (32, 'LENGTH',       'Length',             'Number',  'feet',    NULL, 1),
        (33, 'NIGHTVISION',  'Night Vision',       'Boolean', NULL,      NULL, 1),
        (34, 'POE',          'POE',                'Boolean', NULL,      NULL, 1),
        (35, 'LENS',         'Lens',               'Number',  'mm',      NULL, 1),
        (36, 'MATERIAL',     'Material',           'Text',    NULL,      NULL, 1),
        (37, 'COLOR',        'Colour',             'Text',    NULL,      NULL, 1),
        (38, 'DIMENSIONS',   'Dimensions',         'Text',    NULL,      NULL, 1);
    SET IDENTITY_INSERT dbo.AttributeDefinition OFF;
END
GO

/* --------------------------------------------------------------------------
   §4.8 AttributeTemplate
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.AttributeTemplate)
BEGIN
    SET IDENTITY_INSERT dbo.AttributeTemplate ON;
    INSERT dbo.AttributeTemplate (AttributeTemplateId, TemplateCode, TemplateName, CreatedBy)
    VALUES
        (1,  'FABRIC',     'Fabric',              1),
        (2,  'LAPTOP',     'Laptop',              1),
        (3,  'SERVER',     'Server',              1),
        (4,  'ROUTER',     'Router',              1),
        (5,  'SWITCH',     'Network Switch',      1),
        (6,  'CEMENT',     'Cement',              1),
        (7,  'STEELROD',   'Steel Rod',           1),
        (8,  'CCTV',       'CCTV Camera',         1),
        (9,  'GENERIC',    'Generic Consumable',  1),
        (10, 'OFFICEFURN', 'Office Furniture',    1);
    SET IDENTITY_INSERT dbo.AttributeTemplate OFF;
END
GO

/* --------------------------------------------------------------------------
   §4.9 TemplateAttribute — the §3.9 templates, attribute by attribute.
   The Laptop block (TemplateId 2) reproduces the §4.9 sample table exactly.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.TemplateAttribute)
INSERT dbo.TemplateAttribute (AttributeTemplateId, AttributeDefinitionId, IsRequired, DisplayOrder, CreatedBy)
VALUES
    /* 1 — Fabric (§3.9.1) */
    (1, 4, 1, 1, 1), (1, 3, 1, 2, 1), (1, 5, 1, 3, 1), (1, 16, 0, 4, 1), (1, 17, 0, 5, 1),
    /* 2 — Laptop (§3.9.2 / §4.9) */
    (2, 1, 1, 1, 1), (2, 2, 1, 2, 1), (2, 8, 1, 3, 1), (2, 7, 1, 4, 1), (2, 11, 1, 5, 1),
    (2, 12, 1, 6, 1), (2, 13, 1, 7, 1), (2, 6, 0, 8, 1), (2, 14, 0, 9, 1), (2, 15, 0, 10, 1),
    /* 3 — Server (§3.9.3) */
    (3, 1, 1, 1, 1), (3, 2, 1, 2, 1), (3, 18, 1, 3, 1), (3, 7, 1, 4, 1), (3, 19, 1, 5, 1),
    (3, 20, 0, 6, 1), (3, 21, 0, 7, 1), (3, 22, 0, 8, 1), (3, 6, 0, 9, 1),
    /* 4 — Router (§3.9.4) */
    (4, 1, 1, 1, 1), (4, 23, 1, 2, 1), (4, 24, 1, 3, 1), (4, 13, 1, 4, 1), (4, 25, 0, 5, 1), (4, 26, 0, 6, 1),
    /* 5 — Network Switch */
    (5, 1, 1, 1, 1), (5, 2, 1, 2, 1), (5, 23, 1, 3, 1), (5, 24, 0, 4, 1), (5, 25, 0, 5, 1), (5, 26, 0, 6, 1),
    /* 6 — Cement (§3.9.5) */
    (6, 1, 1, 1, 1), (6, 9, 1, 2, 1), (6, 27, 1, 3, 1), (6, 28, 1, 4, 1), (6, 29, 0, 5, 1), (6, 30, 0, 6, 1),
    /* 7 — Steel Rod (§3.9.6) */
    (7, 31, 1, 1, 1), (7, 32, 1, 2, 1), (7, 9, 1, 3, 1), (7, 28, 1, 4, 1), (7, 27, 0, 5, 1),
    /* 8 — CCTV Camera (§3.9.7) */
    (8, 1, 1, 1, 1), (8, 10, 1, 2, 1), (8, 33, 0, 3, 1), (8, 34, 0, 4, 1), (8, 35, 0, 5, 1),
    /* 9 — Generic Consumable */
    (9, 1, 0, 1, 1), (9, 2, 0, 2, 1), (9, 28, 0, 3, 1),
    /* 10 — Office Furniture */
    (10, 1, 0, 1, 1), (10, 36, 1, 2, 1), (10, 37, 0, 3, 1), (10, 38, 0, 4, 1), (10, 6, 0, 5, 1);
GO

/* --------------------------------------------------------------------------
   §4.3 ItemCategory
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemCategory)
BEGIN
    SET IDENTITY_INSERT dbo.ItemCategory ON;
    INSERT dbo.ItemCategory (ItemCategoryId, CategoryCode, CategoryName, Nature, DisplayOrder, CreatedBy)
    VALUES
        (1,  'RAW', 'Raw Material',          'Material', 1,  1),
        (2,  'SFG', 'Semi Finished',         'Material', 2,  1),
        (3,  'FG',  'Finished Goods',        'Goods',    3,  1),
        (4,  'CON', 'Consumable',            'Material', 4,  1),
        (5,  'SPR', 'Spare Parts',           'Material', 5,  1),
        (6,  'ICT', 'ICT Equipment',         'Asset',    6,  1),
        (7,  'CNS', 'Construction Material', 'Material', 7,  1),
        (8,  'AST', 'Fixed Asset',           'Asset',    8,  1),
        (9,  'SRV', 'Service',               'Service',  9,  1),
        (10, 'OFF', 'Office Supplies',       'Material', 10, 1);
    SET IDENTITY_INSERT dbo.ItemCategory OFF;
END
GO

/* §4.4 ItemGroup */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemGroup)
BEGIN
    SET IDENTITY_INSERT dbo.ItemGroup ON;
    INSERT dbo.ItemGroup (ItemGroupId, ItemCategoryId, GroupCode, GroupName, DisplayOrder, CreatedBy)
    VALUES
        (1,  1, 'FAB', 'Fabric',        1,  1),
        (2,  1, 'TRM', 'Trims',         2,  1),
        (3,  1, 'CHM', 'Chemical',      3,  1),
        (4,  5, 'MAC', 'Machine Spare', 4,  1),
        (5,  6, 'NET', 'Networking',    5,  1),
        (6,  6, 'CMP', 'Computer',      6,  1),
        (7,  6, 'SEC', 'Security',      7,  1),
        (8,  7, 'CEM', 'Cement',        8,  1),
        (9,  7, 'STL', 'Steel',         9,  1),
        (10, 7, 'ELE', 'Electrical',    10, 1);
    SET IDENTITY_INSERT dbo.ItemGroup OFF;
END
GO

/* §4.5 ItemSubGroup */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemSubGroup)
BEGIN
    SET IDENTITY_INSERT dbo.ItemSubGroup ON;
    INSERT dbo.ItemSubGroup (ItemSubGroupId, ItemGroupId, SubGroupCode, SubGroupName, DisplayOrder, CreatedBy)
    VALUES
        (1,  1,  'KNT', 'Knitted Fabric',   1,  1),
        (2,  1,  'WVN', 'Woven Fabric',     2,  1),
        (3,  2,  'ZIP', 'Zipper',           3,  1),
        (4,  2,  'BTN', 'Button',           4,  1),
        (5,  5,  'SWT', 'Network Switch',   5,  1),
        (6,  5,  'RTR', 'Router',           6,  1),
        (7,  6,  'LTP', 'Laptop',           7,  1),
        (8,  6,  'SRV', 'Server',           8,  1),
        (9,  8,  'OPC', 'OPC Cement',       9,  1),
        (10, 10, 'CAB', 'Electrical Cable', 10, 1);
    SET IDENTITY_INSERT dbo.ItemSubGroup OFF;
END
GO

/* §4.6 ItemFamily — DefaultAttributeTemplateId wires each family to its §3.9 template. */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemFamily)
BEGIN
    SET IDENTITY_INSERT dbo.ItemFamily ON;
    INSERT dbo.ItemFamily (ItemFamilyId, ItemSubGroupId, FamilyCode, FamilyName, DefaultAttributeTemplateId, DisplayOrder, CreatedBy)
    VALUES
        (1,  1,  'SJ',    'Single Jersey',        1, 1,  1),
        (2,  1,  'RIB',   'Rib',                  1, 2,  1),
        (3,  6,  'CISCO', 'Cisco',                4, 3,  1),
        (4,  6,  'MIK',   'MikroTik',             4, 4,  1),
        (5,  5,  'HP',    'HPE Aruba',            5, 5,  1),
        (6,  7,  'DELL',  'Dell',                 2, 6,  1),
        (7,  7,  'LEN',   'Lenovo',               2, 7,  1),
        (8,  8,  'HPE',   'HPE ProLiant',         3, 8,  1),
        (9,  9,  'BSC',   'Bashundhara Cement',   6, 9,  1),
        (10, 10, 'RR',    'RR Kabel',             9, 10, 1);
    SET IDENTITY_INSERT dbo.ItemFamily OFF;
END
GO

/* --------------------------------------------------------------------------
   §6.1.1 Warehouse
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.Warehouse)
BEGIN
    SET IDENTITY_INSERT dbo.Warehouse ON;
    INSERT dbo.Warehouse (WarehouseId, WarehouseCode, WarehouseName, BusinessUnitId, WarehouseType, CreatedBy)
    VALUES
        (1,  'COR-WH1', 'Corporate Central Warehouse',     10, 'Main',           1),
        (2,  'ICT-WH1', 'ICT Equipment Store',              6, 'Main',           1),
        (3,  'CON-WH1', 'Construction Site Store',          7, 'Main',           1),
        (4,  'GAR-WH1', 'Garments Raw Material Store',      1, 'RawMaterial',    1),
        (5,  'GAR-WH2', 'Garments Finished Goods Store',    1, 'FinishedGoods',  1),
        (6,  'HOS-WH1', 'Hospital Central Pharmacy Store', 13, 'Main',           1),
        (7,  'TXT-WH1', 'Textile Mill Store',               2, 'Main',           1),
        (8,  'TEA-WH1', 'Tea Estate Store',                 9, 'Main',           1),
        (9,  'DYE-WH1', 'Dyeing Chemical Store',           11, 'Quarantine',     1),
        (10, 'TRN-WH1', 'Transportation Fleet Store',       8, 'Main',           1);
    SET IDENTITY_INSERT dbo.Warehouse OFF;
END
GO

/* §6.2.1 Supplier */
IF NOT EXISTS (SELECT 1 FROM dbo.Supplier)
BEGIN
    SET IDENTITY_INSERT dbo.Supplier ON;
    INSERT dbo.Supplier (SupplierId, SupplierCode, SupplierName, Country, PaymentTermsDays, CreatedBy)
    VALUES
        (1, 'DELL', 'Dell Technologies',        'United States', 45, 1),
        (2, 'CSRC', 'Computer Source Ltd.',     'Bangladesh',    30, 1),
        (3, 'SMTK', 'Smart Tech Distribution',  'Bangladesh',    30, 1),
        (4, 'BSCM', 'Bashundhara Cement Ltd.',  'Bangladesh',    15, 1),
        (5, 'RRKB', 'RR Kabel Ltd.',            'India',         60, 1);
    SET IDENTITY_INSERT dbo.Supplier OFF;
END
GO

/* §6.3.1 Customer — the two named buyers plus a general list-price placeholder. */
IF NOT EXISTS (SELECT 1 FROM dbo.Customer)
BEGIN
    SET IDENTITY_INSERT dbo.Customer ON;
    INSERT dbo.Customer (CustomerId, CustomerCode, CustomerName, Country, CreatedBy)
    VALUES
        (1, 'RBA', 'Retail Buyer A', 'United Kingdom', 1),
        (2, 'RBB', 'Retail Buyer B', 'Germany',        1);
    SET IDENTITY_INSERT dbo.Customer OFF;
END
GO

/* --------------------------------------------------------------------------
   §7.1.1 QCParameterTemplate / §7.1.2 QCParameter
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.QCParameterTemplate)
BEGIN
    SET IDENTITY_INSERT dbo.QCParameterTemplate ON;
    INSERT dbo.QCParameterTemplate (QCTemplateId, TemplateCode, TemplateName, CreatedBy)
    VALUES
        (1, 'CEMENT-QC',     'Cement Quality Test',                1),
        (2, 'FABRIC-QC',     'Fabric Quality Test',                1),
        (3, 'MEDICINE-QC',   'Pharmaceutical Batch Release Test',  1),
        (4, 'STEEL-QC',      'Steel Rod Mechanical Test',          1),
        (5, 'ELECTRICAL-QC', 'Electrical Cable Safety Test',       1),
        (6, 'ICT-QC',        'ICT Equipment Goods-In Inspection',  1);
    SET IDENTITY_INSERT dbo.QCParameterTemplate OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.QCParameter)
BEGIN
    SET IDENTITY_INSERT dbo.QCParameter ON;
    INSERT dbo.QCParameter (QCParameterId, QCTemplateId, ParameterName, DataType, MinValue, MaxValue, UnitOfMeasure, TestMethod, IsCritical, CreatedBy)
    VALUES
        (1, 1, 'Compressive Strength (28-day)', 'Number', 42.5,  52.5,  'MPa',     'ASTM C150', 1, 1),
        (2, 1, 'Initial Setting Time',          'Number', 45.0,  375.0, 'minutes', 'ASTM C150', 1, 1),
        (3, 2, 'GSM Tolerance',                 'Number', 176.0, 184.0, 'gsm',     'ASTM D3776', 1, 1),
        (4, 2, 'Shrinkage',                     'Number', 0.0,   3.0,   '%',       'AATCC 135', 0, 1),
        (5, 3, 'Assay (Active Ingredient)',     'Number', 95.0,  105.0, '%',       'USP', 1, 1),
        (6, 3, 'Moisture Content',              'Number', 0.0,   5.0,   '%',       'USP', 1, 1),
        (7, 4, 'Yield Strength',                'Number', 60.0,  NULL,  'ksi',     'BSTI', 1, 1),
        (8, 5, 'Insulation Resistance',         'Number', 100.0, NULL,  'MOhm',    'IEC 60227', 1, 1);
    SET IDENTITY_INSERT dbo.QCParameter OFF;
END
GO

/* --------------------------------------------------------------------------
   §8.2.1 ApprovalWorkflowTemplate / §8.2.2 ApprovalStep
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ApprovalWorkflowTemplate)
BEGIN
    SET IDENTITY_INSERT dbo.ApprovalWorkflowTemplate ON;
    INSERT dbo.ApprovalWorkflowTemplate (WorkflowTemplateId, TemplateCode, TemplateName, ItemCategoryId, CreatedBy)
    VALUES
        (1, 'STANDARD-ITEM',    'Standard Two-Step Approval',        NULL, 1),  -- default fallback
        (2, 'ICT-ASSET',        'ICT Equipment Three-Step Approval', 6,    1),  -- ICT Equipment
        (3, 'PHARMA-ITEM',      'Pharmaceutical Item Approval',      NULL, 1),  -- assigned per item
        (4, 'CONSTRUCTION-MAT', 'Construction Material Approval',    7,    1);  -- Construction Material
    SET IDENTITY_INSERT dbo.ApprovalWorkflowTemplate OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ApprovalStep)
BEGIN
    SET IDENTITY_INSERT dbo.ApprovalStep ON;
    INSERT dbo.ApprovalStep (ApprovalStepId, WorkflowTemplateId, StepOrder, ApproverRole, IsMandatory, CreatedBy)
    VALUES
        (1, 1, 1, 'Category Manager',   1, 1),
        (2, 1, 2, 'Finance Controller', 1, 1),
        (3, 2, 1, 'Category Manager',   1, 1),   -- §8.2.2 sample: ICT-ASSET
        (4, 2, 2, 'IT Security Review', 1, 1),
        (5, 2, 3, 'Finance Controller', 1, 1),
        (6, 3, 1, 'Category Manager',   1, 1),
        (7, 3, 2, 'QC Manager',         1, 1),
        (8, 3, 3, 'Finance Controller', 1, 1),
        (9, 4, 1, 'Category Manager',   1, 1),
        (10,4, 2, 'Finance Controller', 1, 1);
    SET IDENTITY_INSERT dbo.ApprovalStep OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '11_seed_reference.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('11_seed_reference.sql');
GO
