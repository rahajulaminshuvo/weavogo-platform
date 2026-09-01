/* =============================================================================
   Universal Item Master — 09_indexes_and_audit_fks.sql
   (a) Performance indexes for the query paths Chapter 10/11 specify.
   (b) The §4.1 audit-column foreign keys (CreatedBy / ModifiedBy → UserAccount),
       applied uniformly to every table that carries the standard audit footprint.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* ==========================================================================
   (a) Indexes
   ========================================================================== */

/* Classification pick-lists (cascading dropdowns, §11.2.1). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemGroup_Category' AND object_id=OBJECT_ID(N'dbo.ItemGroup'))
    CREATE INDEX IX_ItemGroup_Category ON dbo.ItemGroup (ItemCategoryId, DisplayOrder) INCLUDE (GroupCode, GroupName, IsActive);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemSubGroup_Group' AND object_id=OBJECT_ID(N'dbo.ItemSubGroup'))
    CREATE INDEX IX_ItemSubGroup_Group ON dbo.ItemSubGroup (ItemGroupId, DisplayOrder) INCLUDE (SubGroupCode, SubGroupName, IsActive);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemFamily_SubGroup' AND object_id=OBJECT_ID(N'dbo.ItemFamily'))
    CREATE INDEX IX_ItemFamily_SubGroup ON dbo.ItemFamily (ItemSubGroupId, DisplayOrder) INCLUDE (FamilyCode, FamilyName, DefaultAttributeTemplateId, IsActive);
GO

/* Item search (§10.5): category + status filters, name lookup. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemMaster_CategoryStatus' AND object_id=OBJECT_ID(N'dbo.ItemMaster'))
    CREATE INDEX IX_ItemMaster_CategoryStatus ON dbo.ItemMaster (ItemCategoryId, ItemStatus, IsDeleted) INCLUDE (ItemCode, ItemName);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemMaster_Status' AND object_id=OBJECT_ID(N'dbo.ItemMaster'))
    CREATE INDEX IX_ItemMaster_Status ON dbo.ItemMaster (ItemStatus, IsDeleted) INCLUDE (ItemCode, ItemName, ItemCategoryId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemMaster_Name' AND object_id=OBJECT_ID(N'dbo.ItemMaster'))
    CREATE INDEX IX_ItemMaster_Name ON dbo.ItemMaster (ItemName) INCLUDE (ItemCode, ItemStatus);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemMaster_Family' AND object_id=OBJECT_ID(N'dbo.ItemMaster'))
    CREATE INDEX IX_ItemMaster_Family ON dbo.ItemMaster (ItemFamilyId) INCLUDE (ItemSubGroupId, ItemGroupId, ItemCategoryId);
GO

/* Attribute read path (§10.4). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemAttribute_Item' AND object_id=OBJECT_ID(N'dbo.ItemAttribute'))
    CREATE INDEX IX_ItemAttribute_Item ON dbo.ItemAttribute (ItemId, IsDeleted) INCLUDE (AttributeDefinitionId, ValueText, ValueNumber, ValueDate, ValueBoolean);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_TemplateAttribute_Template' AND object_id=OBJECT_ID(N'dbo.TemplateAttribute'))
    CREATE INDEX IX_TemplateAttribute_Template ON dbo.TemplateAttribute (AttributeTemplateId, DisplayOrder) INCLUDE (AttributeDefinitionId, IsRequired, DefaultValue);
GO

/* Business-unit authorization filter (§10.5 businessEntityId). */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_BusinessUnitItem_Unit' AND object_id=OBJECT_ID(N'dbo.BusinessUnitItem'))
    CREATE INDEX IX_BusinessUnitItem_Unit ON dbo.BusinessUnitItem (BusinessUnitId, IsAuthorized, IsDeleted) INCLUDE (ItemId);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_BusinessUnitItem_Item' AND object_id=OBJECT_ID(N'dbo.BusinessUnitItem'))
    CREATE INDEX IX_BusinessUnitItem_Item ON dbo.BusinessUnitItem (ItemId, IsAuthorized, IsDeleted) INCLUDE (BusinessUnitId);
GO

/* Governance read paths. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemAuditLog_Item' AND object_id=OBJECT_ID(N'dbo.ItemAuditLog'))
    CREATE INDEX IX_ItemAuditLog_Item ON dbo.ItemAuditLog (ItemId, ChangedDate DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemVersion_Item' AND object_id=OBJECT_ID(N'dbo.ItemVersion'))
    CREATE INDEX IX_ItemVersion_Item ON dbo.ItemVersion (ItemId, VersionNumber DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemApprovalRequest_Item' AND object_id=OBJECT_ID(N'dbo.ItemApprovalRequest'))
    CREATE INDEX IX_ItemApprovalRequest_Item ON dbo.ItemApprovalRequest (ItemId, OverallStatus);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ApprovalStep_Template' AND object_id=OBJECT_ID(N'dbo.ApprovalStep'))
    CREATE INDEX IX_ApprovalStep_Template ON dbo.ApprovalStep (WorkflowTemplateId, StepOrder) INCLUDE (ApproverRole, IsMandatory);
GO

/* Traceability & sourcing. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemLot_ItemStatus' AND object_id=OBJECT_ID(N'dbo.ItemLot'))
    CREATE INDEX IX_ItemLot_ItemStatus ON dbo.ItemLot (ItemId, QCStatus, ExpiryDate);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_ItemSerial_Item' AND object_id=OBJECT_ID(N'dbo.ItemSerial'))
    CREATE INDEX IX_ItemSerial_Item ON dbo.ItemSerial (ItemId, Status);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SupplierItem_Item' AND object_id=OBJECT_ID(N'dbo.SupplierItem'))
    CREATE INDEX IX_SupplierItem_Item ON dbo.SupplierItem (ItemId) INCLUDE (SupplierId, IsPreferred, LeadTimeDays);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_SupplierPrice_SupplierItem' AND object_id=OBJECT_ID(N'dbo.SupplierPrice'))
    CREATE INDEX IX_SupplierPrice_SupplierItem ON dbo.SupplierPrice (SupplierItemId, EffectiveFrom DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_WarehouseItem_Warehouse' AND object_id=OBJECT_ID(N'dbo.WarehouseItem'))
    CREATE INDEX IX_WarehouseItem_Warehouse ON dbo.WarehouseItem (WarehouseId) INCLUDE (ItemId, QuantityOnHand, QuantityReserved);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_BOMComponent_Component' AND object_id=OBJECT_ID(N'dbo.BOMComponent'))
    CREATE INDEX IX_BOMComponent_Component ON dbo.BOMComponent (ComponentItemId) INCLUDE (BOMId);
GO

/* ==========================================================================
   (b) §4.1 audit-column foreign keys, applied to every table that has them.
       Generated rather than hand-written so no table is accidentally missed.
   ========================================================================== */
DECLARE @sql NVARCHAR(MAX) = N'';

SELECT @sql = @sql + N'
ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    ADD CONSTRAINT ' + QUOTENAME(N'FK_' + t.name + N'_CreatedBy') + N'
        FOREIGN KEY (CreatedBy) REFERENCES dbo.UserAccount (UserId);'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = 'CreatedBy')
  AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = t.object_id
                  AND fk.name = N'FK_' + t.name + N'_CreatedBy');

SELECT @sql = @sql + N'
ALTER TABLE ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name) + N'
    ADD CONSTRAINT ' + QUOTENAME(N'FK_' + t.name + N'_ModifiedBy') + N'
        FOREIGN KEY (ModifiedBy) REFERENCES dbo.UserAccount (UserId);'
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE EXISTS (SELECT 1 FROM sys.columns c WHERE c.object_id = t.object_id AND c.name = 'ModifiedBy')
  AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys fk WHERE fk.parent_object_id = t.object_id
                  AND fk.name = N'FK_' + t.name + N'_ModifiedBy');

EXEC sp_executesql @sql;
GO

/* ItemAuditLog.ChangedBy (§8.3.1). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ItemAuditLog_ChangedBy')
    ALTER TABLE dbo.ItemAuditLog
        ADD CONSTRAINT FK_ItemAuditLog_ChangedBy FOREIGN KEY (ChangedBy) REFERENCES dbo.UserAccount (UserId);
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '09_indexes_and_audit_fks.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('09_indexes_and_audit_fks.sql');
GO
