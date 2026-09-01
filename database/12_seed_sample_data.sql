/* =============================================================================
   Universal Item Master — 12_seed_sample_data.sql
   The worked examples from the SDS, loaded as real, FK-consistent rows so the
   API and UI have something to run against on a fresh database.

   Sources: §4.10, §4.11, §5.3, §5.6, §6.1.2–6.6.1, §7.1.3–7.4.1, §8.2.3–8.5.1.

   TWO DELIBERATE EXTENSIONS, both marked "[extension]" below:
     1. Classification rows with Ids 11+ (groups/sub-groups/families for apparel,
        pharma, medical, material-handling, power and connectivity). The SDS's
        Chapter 6–8 examples reference a T-Shirt, Paracetamol, an MRI scanner, a
        forklift, a generator and an internet subscription, but its Chapter 4
        reference dataset stops at ten families. These rows exist only so those
        named examples have a valid ItemFamilyId — no new table or column.
     2. Items are inserted with their final ItemStatus. §8.1 forbids a *transition*
        from Draft straight to Active; it does not forbid loading already-approved
        history, which is exactly what migration (§13.4) does.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* --------------------------------------------------------------------------
   [extension] Classification rows for the Chapter 6–8 worked examples
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemGroup WHERE ItemGroupId = 11)
BEGIN
    SET IDENTITY_INSERT dbo.ItemGroup ON;
    INSERT dbo.ItemGroup (ItemGroupId, ItemCategoryId, GroupCode, GroupName, DisplayOrder, CreatedBy)
    VALUES
        (11, 3, 'APR', 'Apparel',             11, 1),
        (12, 4, 'PHR', 'Pharmaceutical',      12, 1),
        (13, 8, 'MED', 'Medical Equipment',   13, 1),
        (14, 8, 'MHE', 'Material Handling',   14, 1),
        (15, 8, 'PWR', 'Power Equipment',     15, 1),
        (16, 9, 'CNT', 'Connectivity',        16, 1);
    SET IDENTITY_INSERT dbo.ItemGroup OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemSubGroup WHERE ItemSubGroupId = 11)
BEGIN
    SET IDENTITY_INSERT dbo.ItemSubGroup ON;
    INSERT dbo.ItemSubGroup (ItemSubGroupId, ItemGroupId, SubGroupCode, SubGroupName, DisplayOrder, CreatedBy)
    VALUES
        (11, 11, 'TSH', 'T-Shirt',        11, 1),
        (12, 12, 'TAB', 'Tablet',         12, 1),
        (13, 13, 'MRI', 'MRI Scanner',    13, 1),
        (14, 14, 'FLT', 'Forklift',       14, 1),
        (15, 15, 'GEN', 'Generator',      15, 1),
        (16, 16, 'INT', 'Internet Link',  16, 1);
    SET IDENTITY_INSERT dbo.ItemSubGroup OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemFamily WHERE ItemFamilyId = 11)
BEGIN
    SET IDENTITY_INSERT dbo.ItemFamily ON;
    INSERT dbo.ItemFamily (ItemFamilyId, ItemSubGroupId, FamilyCode, FamilyName, DefaultAttributeTemplateId, DisplayOrder, CreatedBy)
    VALUES
        (11, 11, 'BASIC', 'Basic Tee',         9, 11, 1),
        (12, 3,  'YKK',   'YKK',               9, 12, 1),
        (13, 12, 'PARA',  'Paracetamol',       9, 13, 1),
        (14, 13, 'SIE',   'Siemens',           9, 14, 1),
        (15, 14, 'TOY',   'Toyota',            9, 15, 1),
        (16, 15, 'DGEN',  'Diesel Generator',  9, 16, 1),
        (17, 16, 'ISP',   'ISP Service',       9, 17, 1),
        (18, 5,  'CSCS',  'Cisco Switch',      5, 18, 1);
    SET IDENTITY_INSERT dbo.ItemFamily OFF;
END
GO

/* --------------------------------------------------------------------------
   §4.10 ItemMaster — the ten reference items, plus the items the Chapter 6–8
   examples name. Denormalized ancestry is set to the true ancestors of each
   ItemFamilyId (TR_ItemMaster_ValidateAncestry rejects anything else).
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemMaster)
BEGIN
    SET IDENTITY_INSERT dbo.ItemMaster ON;
    INSERT dbo.ItemMaster
        (ItemId, ItemCode, ItemName, Description,
         ItemCategoryId, ItemGroupId, ItemSubGroupId, ItemFamilyId, AttributeTemplateId, BaseUOMId,
         CanPurchase, CanSell, CanManufacture, CanStock, CanTransfer,
         IsSerialControlled, IsLotControlled, ItemStatus, VersionNumber, CreatedBy)
    VALUES
        (1,  'MAT-FAB-000001', N'100% Cotton Single Jersey 180 GSM', N'Knitted single jersey, bio-washed finish.',
             1, 1, 1, 1, 1, 3,   1,0,0,1,1, 0,1, 'Active', 1, 101),
        (2,  'MAT-FAB-000002', N'95/5 Rib Fabric', N'Cotton/elastane rib for collars and cuffs.',
             1, 1, 1, 2, 1, 3,   1,0,0,1,1, 0,1, 'Active', 1, 101),
        (3,  'ICT-NET-000022', N'Cisco ISR4331 Router', N'Integrated services router, 3 ports.',
             6, 5, 6, 3, 4, 1,   1,0,0,1,1, 1,0, 'Active', 1, 101),
        (4,  'ICT-NET-000009', N'MikroTik CCR2116 Router', N'Cloud core router, RouterOS v7.',
             6, 5, 6, 4, 4, 1,   1,0,0,1,1, 1,0, 'Active', 1, 101),
        (5,  'ICT-NET-000023', N'HPE Aruba 2930F Switch 24-Port', N'Layer-3 access switch.',
             6, 5, 5, 5, 5, 1,   1,0,0,1,1, 1,0, 'Active', 1, 101),
        (6,  'ICT-LTP-000006', N'Dell Latitude 7450 Laptop', N'Corporate standard business laptop.',
             6, 6, 7, 6, 2, 1,   1,0,0,1,1, 1,0, 'Active', 1, 101),
        (7,  'ICT-LTP-000021', N'Lenovo ThinkPad X1 Carbon', N'Executive ultrabook, pending approval.',
             6, 6, 7, 7, 2, 1,   1,0,0,1,1, 1,0, 'PendingApproval', 1, 101),
        (8,  'ICT-SRV-000007', N'HPE ProLiant DL380 Gen11 Server', N'2U rack server.',
             6, 6, 8, 8, 3, 1,   1,0,0,1,1, 1,0, 'Active', 1, 101),
        (9,  'CON-CEM-000013', N'Bashundhara OPC Cement 50 KG', N'Ordinary Portland Cement, 50 kg bag.',
             7, 8, 9, 9, 6, 8,   1,0,0,1,1, 0,1, 'Active', 1, 101),
        (10, 'CON-ELE-000019', N'RR PVC Cable 2.5 sqmm', N'PVC insulated copper cable.',
             7, 10, 10, 10, 9, 3, 1,0,0,1,1, 0,0, 'Active', 1, 101),
        /* [extension] items referenced by the Chapter 6–8 worked examples */
        (11, 'FG-APR-000011',  N'Cotton Jersey T-Shirt', N'Finished good; BOM parent (§6.5).',
             3, 11, 11, 11, 9, 1, 0,1,1,1,1, 0,0, 'Active', 1, 101),
        (12, 'MAT-TRM-000012', N'YKK Nylon Zipper 8 Inch', N'Trim component.',
             1, 2, 3, 12, 9, 1, 1,0,0,1,1, 0,0, 'Active', 1, 101),
        (13, 'CON-PHR-000013', N'Paracetamol 500mg', N'Lot- and expiry-controlled medicine (§7.5.1).',
             4, 12, 12, 13, 9, 1, 1,0,0,1,1, 0,1, 'Active', 1, 101),
        (14, 'AST-MED-000014', N'Siemens MRI Scanner 1.5T', N'Serial- and calibration-controlled asset (§7.5.2).',
             8, 13, 13, 14, 9, 1, 1,0,0,1,1, 1,0, 'Active', 1, 101),
        (15, 'AST-MHE-000015', N'Toyota Forklift 3 Ton', N'Capitalized material-handling asset.',
             8, 14, 14, 15, 9, 1, 1,0,0,1,1, 1,0, 'Active', 1, 101),
        (16, 'AST-PWR-000016', N'Diesel Generator 100 KVA', N'Capitalized standby power asset.',
             8, 15, 15, 16, 9, 1, 1,0,0,1,1, 1,0, 'Active', 1, 101),
        (17, 'SRV-CNT-000017', N'Internet Bandwidth 100 Mbps', N'Service item: CanStock = 0 (§5.1).',
             9, 16, 16, 17, 9, 1, 1,0,0,0,0, 0,0, 'Active', 1, 101),
        (18, 'ICT-NET-000018', N'Cisco Catalyst 9200 Switch', N'Legacy switch, end-of-sale (§8.5.1).',
             6, 5, 5, 18, 5, 1, 1,0,0,1,1, 1,0, 'Obsolete', 1, 101);
    SET IDENTITY_INSERT dbo.ItemMaster OFF;
END
GO

/* --------------------------------------------------------------------------
   §4.11 ItemAttribute — the Dell Latitude 7450 value set, plus a fabric, a
   router and a cement example to exercise Number/Text/Boolean/Enum/Date.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemAttribute)
INSERT dbo.ItemAttribute (ItemId, AttributeDefinitionId, ValueText, ValueNumber, ValueDate, ValueBoolean, CreatedBy)
VALUES
    /* ItemId 6 — Dell Latitude 7450 (§4.11 sample) */
    (6,  1,  N'Dell',               NULL, NULL, NULL, 101),
    (6,  2,  N'Latitude 7450',      NULL, NULL, NULL, 101),
    (6,  8,  N'Intel Core Ultra 7', NULL, NULL, NULL, 101),
    (6,  7,  NULL,                  32,   NULL, NULL, 101),
    (6,  11, NULL,                  1,    NULL, NULL, 101),
    (6,  12, N'14" FHD',            NULL, NULL, NULL, 101),
    (6,  13, N'Windows 11 Pro',     NULL, NULL, NULL, 101),
    (6,  6,  NULL,                  3,    NULL, NULL, 101),
    /* ItemId 1 — Single Jersey fabric (§3.9.1) */
    (1,  4,  N'100% Cotton',        NULL, NULL, NULL, 101),
    (1,  3,  NULL,                  180,  NULL, NULL, 101),
    (1,  5,  NULL,                  72,   NULL, NULL, 101),
    (1,  16, NULL,                  3,    NULL, NULL, 101),
    (1,  17, N'Bio Wash',           NULL, NULL, NULL, 101),
    /* ItemId 4 — MikroTik router (§3.9.4): Boolean and Text mix */
    (4,  1,  N'MikroTik',           NULL, NULL, NULL, 101),
    (4,  23, NULL,                  13,   NULL, NULL, 101),
    (4,  24, N'100 Gbps',           NULL, NULL, NULL, 101),
    (4,  13, N'RouterOS v7',        NULL, NULL, NULL, 101),
    (4,  25, NULL,                  NULL, NULL, 1,    101),
    (4,  26, N'Dual PSU',           NULL, NULL, NULL, 101),
    /* ItemId 8 — HPE ProLiant server: exercises the Enum attribute RACKUNIT */
    (8,  1,  N'HPE',                NULL, NULL, NULL, 101),
    (8,  2,  N'DL380 Gen11',        NULL, NULL, NULL, 101),
    (8,  18, N'2 x Intel Xeon Gold',NULL, NULL, NULL, 101),
    (8,  7,  NULL,                  128,  NULL, NULL, 101),
    (8,  19, N'4 x 1.92 TB SSD',    NULL, NULL, NULL, 101),
    (8,  22, N'2U',                 NULL, NULL, NULL, 101),
    (8,  6,  NULL,                  5,    NULL, NULL, 101),
    /* ItemId 9 — Cement (§3.9.5): exercises the Date attribute */
    (9,  1,  N'Bashundhara',        NULL, NULL, NULL, 101),
    (9,  9,  N'OPC',                NULL, NULL, NULL, 101),
    (9,  27, NULL,                  50,   NULL, NULL, 101),
    (9,  28, N'ASTM C150',          NULL, NULL, NULL, 101),
    (9,  29, NULL,                  NULL, '2026-07-15', NULL, 101),
    (9,  30, NULL,                  90,   NULL, NULL, 101);
GO

/* --------------------------------------------------------------------------
   §5.3 BusinessUnitItem — the two five-unit authorization sets from the SDS,
   plus baseline authorizations for the remaining items.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BusinessUnitItem)
INSERT dbo.BusinessUnitItem (ItemId, BusinessUnitId, IsAuthorized, AuthorizedDate, CreatedBy)
VALUES
    /* Dell Latitude 7450 (ItemId 6) — Corporate, ICT, Construction, Hospital, Garments */
    (6, 10, 1, '2026-01-05', 101), (6, 6, 1, '2026-01-05', 101), (6, 7, 1, '2026-01-05', 101),
    (6, 13, 1, '2026-01-05', 101), (6, 1, 1, '2026-01-05', 101),
    /* Cisco ISR4331 Router (ItemId 3) — a different five-unit combination */
    (3, 10, 1, '2026-01-05', 101), (3, 1, 1, '2026-01-05', 101), (3, 2, 1, '2026-01-05', 101),
    (3, 9, 1, '2026-01-05', 101), (3, 13, 1, '2026-01-05', 101),
    /* Fabrics and trims — garments and textile only */
    (1, 1, 1, '2026-01-05', 101), (1, 2, 1, '2026-01-05', 101),
    (2, 1, 1, '2026-01-05', 101), (12, 1, 1, '2026-01-05', 101),
    (11, 1, 1, '2026-01-05', 101),
    /* Construction materials */
    (9, 7, 1, '2026-01-05', 101), (10, 7, 1, '2026-01-05', 101),
    /* ICT */
    (4, 6, 1, '2026-01-05', 101), (5, 6, 1, '2026-01-05', 101),
    (7, 6, 1, '2026-01-05', 101), (8, 6, 1, '2026-01-05', 101), (18, 6, 0, '2026-01-05', 101),
    /* Hospital */
    (13, 13, 1, '2026-01-05', 101), (14, 13, 1, '2026-01-05', 101),
    /* Assets and services, corporate-held */
    (15, 8, 1, '2026-01-05', 101), (16, 10, 1, '2026-01-05', 101), (17, 10, 1, '2026-01-05', 101);
GO

/* --------------------------------------------------------------------------
   §5.6 ItemPackaging
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemPackaging)
BEGIN
    SET IDENTITY_INSERT dbo.ItemPackaging ON;
    INSERT dbo.ItemPackaging (ItemPackagingId, ItemId, PackagingLevel, PackagingUOMId, QtyPerParentLevel, IsPurchaseUOM, IsSalesUOM, CreatedBy)
    VALUES
        (1, 6, 1, 1, 1, 0, 1, 101),   -- Dell Latitude: Each (PCS), sales UOM
        (2, 6, 3, 7, 5, 1, 0, 101),   -- Dell Latitude: Carton of 5 (CTN), purchase UOM
        (3, 9, 1, 8, 1, 1, 1, 101);   -- Cement: Each = one 50 KG bag, both
    SET IDENTITY_INSERT dbo.ItemPackaging OFF;
END
GO

/* --------------------------------------------------------------------------
   §6.1.2 WarehouseItem / §6.1.3 InventoryPolicy
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.WarehouseItem)
INSERT dbo.WarehouseItem (ItemId, WarehouseId, QuantityOnHand, QuantityReserved, CreatedBy)
VALUES
    (6, 1, 35, 0, 104), (6, 2, 30, 5, 104), (6, 3, 10, 0, 104), (6, 6, 5, 0, 104), (6, 4, 20, 0, 104),
    (9, 3, 1200, 0, 104), (1, 4, 4500, 250, 104), (10, 3, 2600, 0, 104), (13, 6, 8000, 0, 104);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.InventoryPolicy)
INSERT dbo.InventoryPolicy (ItemId, BusinessUnitId, ReorderLevel, SafetyStock, ABCClass, CreatedBy)
VALUES
    (6,  6, 10,   5,   'A', 101),
    (9,  7, 200,  50,  'A', 101),
    (10, 7, 500,  100, 'B', 101),
    (1,  1, 1000, 200, 'A', 101);
GO

/* --------------------------------------------------------------------------
   §6.2.2 SupplierItem / §6.2.3 SupplierPrice
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.SupplierItem)
BEGIN
    SET IDENTITY_INSERT dbo.SupplierItem ON;
    INSERT dbo.SupplierItem (SupplierItemId, ItemId, SupplierId, SupplierPartNumber, PurchaseUOMId, IsPreferred, LeadTimeDays, MinOrderQty, CreatedBy)
    VALUES
        (1, 6,  1, 'LAT7450',    7, 1, 21, 5,   101),   -- Dell, cartons of 5
        (2, 6,  2, 'DL7450',     1, 0, 7,  1,   101),
        (3, 6,  3, 'DELL7450',   1, 0, 5,  1,   101),
        (4, 9,  4, 'OPC-50KG',   8, 1, 2,  100, 101),
        (5, 10, 5, 'RR-PVC-2.5', 3, 1, 14, 500, 101);
    SET IDENTITY_INSERT dbo.SupplierItem OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SupplierPrice)
INSERT dbo.SupplierPrice (SupplierItemId, UnitPrice, Currency, EffectiveFrom, CreatedBy)
VALUES
    (1, 1200.0000, 'USD', '2026-01-01', 102),
    (2, 1180.0000, 'USD', '2026-01-01', 102),
    (3, 1225.0000, 'USD', '2026-01-01', 102),
    (4,    7.5000, 'USD', '2026-06-01', 102);
GO

/* --------------------------------------------------------------------------
   §6.3.1 CustomerItem / SalesPrice
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.CustomerItem)
BEGIN
    SET IDENTITY_INSERT dbo.CustomerItem ON;
    INSERT dbo.CustomerItem (CustomerItemId, ItemId, CustomerId, CustomerItemCode, IsAuthorized, CreatedBy)
    VALUES
        (1, 11, 1, 'RBA-TEE-001', 1, 105),
        (2, 11, 2, 'RBB-TS-9920', 1, 105);
    SET IDENTITY_INSERT dbo.CustomerItem OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SalesPrice)
INSERT dbo.SalesPrice (ItemId, CustomerItemId, CustomerId, UnitPrice, Currency, EffectiveFrom, CreatedBy)
VALUES
    (11, NULL, NULL, 4.5000, 'USD', '2026-01-01', 105),   -- general list price
    (11, 1,    1,    4.2000, 'USD', '2026-01-01', 105),   -- Retail Buyer A
    (11, 2,    2,    4.3500, 'USD', '2026-01-01', 105);   -- Retail Buyer B
GO

/* --------------------------------------------------------------------------
   §6.4.1 GLMapping / §6.4.2 TaxMapping
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.GLMapping)
INSERT dbo.GLMapping (ItemCategoryId, ItemId, BusinessUnitId, InventoryGLCode, COGSGLCode, SalesGLCode, PurchaseGLCode, CreatedBy)
VALUES
    (6,    NULL, 6,  '1310', '5310', NULL,   '2110', 102),
    (7,    NULL, 7,  '1320', '5320', NULL,   '2120', 102),
    (1,    NULL, 1,  '1300', '5300', NULL,   '2100', 102),
    (NULL, 6,    13, '1315', '5315', NULL,   '2115', 102);   -- item-level override
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TaxMapping)
INSERT dbo.TaxMapping (ItemCategoryId, ItemId, Jurisdiction, TaxCode, TaxRatePercent, TaxType, CreatedBy)
VALUES
    (6,    NULL, 'Bangladesh', 'VAT-STD', 15.00, 'VAT',    102),
    (7,    NULL, 'Bangladesh', 'VAT-STD', 15.00, 'VAT',    102),
    (9,    NULL, 'Bangladesh', 'VAT-SRV',  7.50, 'VAT',    102),
    (NULL, 13,   'Bangladesh', 'VAT-EXM',  0.00, 'Exempt', 102);   -- Paracetamol override
GO

/* --------------------------------------------------------------------------
   §6.5 BillOfMaterial / BOMComponent — the T-Shirt recipe
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.BillOfMaterial)
BEGIN
    SET IDENTITY_INSERT dbo.BillOfMaterial ON;
    INSERT dbo.BillOfMaterial (BOMId, ParentItemId, BOMVersion, EffectiveDate, Status, CreatedBy)
    VALUES (1, 11, 1, '2026-01-01', 'Active', 101);
    SET IDENTITY_INSERT dbo.BillOfMaterial OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.BOMComponent)
INSERT dbo.BOMComponent (BOMId, ComponentItemId, QtyPerUnit, UOMId, ScrapPercent, CreatedBy)
VALUES
    (1, 1,  0.850000, 3, 3.00, 101),   -- Single Jersey, MTR
    (1, 2,  0.100000, 3, 3.00, 101),   -- Rib fabric, MTR
    (1, 12, 0.000000, 1, 1.00, 101);   -- Zipper, PCS (qty per §6.5.2 sample)
GO

/* --------------------------------------------------------------------------
   §6.6.1 AssetMaster
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.AssetMaster)
BEGIN
    SET IDENTITY_INSERT dbo.AssetMaster ON;
    INSERT dbo.AssetMaster (AssetId, ItemId, AssetCode, CapitalizationDate, AcquisitionCost, DepreciationMethod, UsefulLifeMonths, AssetStatus, BusinessUnitId, CreatedBy)
    VALUES
        (1, 15, 'AST-000001', '2026-02-01', 28500.00, 'StraightLine',     120, 'InUse', 8,  102),
        (2, 16, 'AST-000002', '2026-02-01', 15200.00, 'StraightLine',     120, 'InUse', 10, 102),
        (3, 8,  'AST-000003', '2026-03-01',  9800.00, 'DecliningBalance',  60, 'InUse', 6,  102),
        (4, 14, 'AST-000004', '2026-03-15', 750000.00,'StraightLine',     180, 'InUse', 13, 102);
    SET IDENTITY_INSERT dbo.AssetMaster OFF;
END
GO

/* --------------------------------------------------------------------------
   §7.1.3 ItemQCProfile
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemQCProfile)
INSERT dbo.ItemQCProfile (ItemFamilyId, ItemId, QCTemplateId, IsMandatory, InspectionFrequency, CreatedBy)
VALUES
    (9,    NULL, 1, 1, 'EveryBatch', 103),   -- Family: Bashundhara Cement
    (1,    NULL, 2, 1, 'Sampling',   103),   -- Family: Single Jersey
    (NULL, 13,   3, 1, 'EveryBatch', 103);   -- Item: Paracetamol 500mg
GO

/* --------------------------------------------------------------------------
   §7.2.1 ItemLot / §7.2.2 ItemSerial
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemLot)
INSERT dbo.ItemLot (ItemId, LotNumber, ManufactureDate, ExpiryDate, WarehouseId, QuantityReceived, QCStatus, CreatedBy)
VALUES
    (9,  'BSC-2026-0715', '2026-07-15', '2026-10-13', 3, 1200,  'Passed',  103),
    (13, 'PARA-2026-A12', '2026-03-01', '2028-03-31', 6, 5000,  'Passed',  103),
    (13, 'PARA-2026-A13', '2026-04-01', '2028-04-30', 6, 3000,  'Pending', 103),
    (1,  'SJ-2026-0601',  '2026-06-01', NULL,         4, 4500,  'Passed',  103);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemSerial)
BEGIN
    SET IDENTITY_INSERT dbo.ItemSerial ON;
    INSERT dbo.ItemSerial (ItemSerialId, ItemId, SerialNumber, WarehouseId, Status, WarrantyExpiryDate, LastCalibrationDate, NextCalibrationDueDate, LinkedAssetId, CreatedBy)
    VALUES
        (1, 8,  'SN-HPE-88213', 2,    'InStock', '2031-03-01', NULL,         NULL,         3, 104),
        (2, 14, 'SN-MRI-04471', NULL, 'Issued',  '2031-03-15', '2025-11-01', '2026-11-01', 4, 104),
        (3, 15, 'SN-FLT-22190', NULL, 'Issued',  '2029-02-01', NULL,         NULL,         1, 104);
    SET IDENTITY_INSERT dbo.ItemSerial OFF;
END
GO

/* --------------------------------------------------------------------------
   §7.3 ItemBarcode / ItemRFID
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemBarcode)
INSERT dbo.ItemBarcode (ItemId, ItemPackagingId, BarcodeType, BarcodeValue, IsPrimary, CreatedBy)
VALUES
    (6, 1, 'EAN13',   '8801643123456', 1, 104),
    (6, 2, 'Code128', 'CTN-DL7450-05', 0, 104),
    (9, 3, 'EAN13',   '8801643789012', 1, 104);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemRFID)
INSERT dbo.ItemRFID (ItemId, ItemSerialId, EPCCode, TagType, AssignedDate, CreatedBy)
VALUES
    (15, 3, 'E200-3412-0891-4471', 'Active', '2026-02-05', 104),
    (14, 2, 'E200-3412-0891-9902', 'Active', '2026-03-16', 104);
GO

/* --------------------------------------------------------------------------
   §7.4.1 ItemDocument
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemDocument)
INSERT dbo.ItemDocument (ItemId, DocumentType, FileName, FileURL, Version, UploadedDate, CreatedBy)
VALUES
    (14, 'ComplianceCertificate', 'MRI-Calibration-Cert-2026.pdf',      '/docs/items/14/MRI-Calibration-Cert-2026.pdf',      '1.0', '2026-03-16', 103),
    (9,  'MSDS',                  'Bashundhara-Cement-MSDS.pdf',        '/docs/items/9/Bashundhara-Cement-MSDS.pdf',         '2.1', '2026-06-02', 103),
    (6,  'Datasheet',             'Dell-Latitude-7450-Spec.pdf',        '/docs/items/6/Dell-Latitude-7450-Spec.pdf',         '1.0', '2026-01-06', 101),
    (13, 'ComplianceCertificate', 'Paracetamol-BatchRelease-A12.pdf',   '/docs/items/13/Paracetamol-BatchRelease-A12.pdf',   '1.0', '2026-03-05', 103);
GO

/* --------------------------------------------------------------------------
   §8.2.3 ItemApprovalRequest / §8.2.4 ItemApprovalAction
   The Lenovo ThinkPad (ItemId 7) sitting at step 2 of the ICT-ASSET workflow.
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemApprovalRequest)
BEGIN
    SET IDENTITY_INSERT dbo.ItemApprovalRequest ON;
    INSERT dbo.ItemApprovalRequest (RequestId, ItemId, WorkflowTemplateId, RequestedBy, RequestedDate, CurrentStepOrder, OverallStatus, CreatedBy)
    VALUES (1, 7, 2, 101, '2026-08-02T09:15:32', 2, 'Pending', 101);
    SET IDENTITY_INSERT dbo.ItemApprovalRequest OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.ItemApprovalAction)
BEGIN
    SET IDENTITY_INSERT dbo.ItemApprovalAction ON;
    INSERT dbo.ItemApprovalAction (ActionId, RequestId, StepOrder, ActionedBy, ActionDate, Decision, Comments, CreatedBy)
    VALUES (1, 1, 1, 101, '2026-08-02T11:04:10', 'Approved', 'Matches standing laptop spec', 101);
    SET IDENTITY_INSERT dbo.ItemApprovalAction OFF;
END
GO

/* --------------------------------------------------------------------------
   §8.4.1 ItemVersion — the Dell Latitude's two approved versions
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemVersion)
INSERT dbo.ItemVersion (ItemId, VersionNumber, SnapshotJson, ChangeReason, CreatedBy)
VALUES
    (6, 1, N'{"itemId":6,"itemCode":"ICT-LTP-000006","itemName":"Dell Latitude 7450 Laptop","itemStatus":"Active","versionNumber":1,"attributes":[{"attributeCode":"RAM","value":16}]}',
        'Initial approval and activation', 101);
GO

/* --------------------------------------------------------------------------
   §8.5.1 ItemObsolescence — the legacy Catalyst switch superseded by the ISR4331
   -------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM dbo.ItemObsolescence)
BEGIN
    SET IDENTITY_INSERT dbo.ItemObsolescence ON;
    INSERT dbo.ItemObsolescence (ObsolescenceId, ItemId, ReplacementItemId, ObsoleteDate, Reason, CreatedBy)
    VALUES (1, 18, 3, '2026-05-31', 'End-of-sale by manufacturer; superseded model adopted', 101);
    SET IDENTITY_INSERT dbo.ItemObsolescence OFF;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '12_seed_sample_data.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('12_seed_sample_data.sql');
GO
