/* =============================================================================
   Universal Item Master — 10_triggers.sql

   Database-enforced rules that cannot be expressed as CHECK constraints:

     TR_ItemMaster_ValidateAncestry     §4.2 / §4.10 — denormalized ancestry sync
     TR_ItemMaster_StatusTransition     §8.1 / §12.1 rule 4 — lifecycle state machine
     TR_ItemMaster_Audit                §8.3.1 / §12.1 rule 21 — field-level audit
     TR_ItemAttribute_Audit             §8.3.1
     TR_ItemAttribute_ValueDataType     §4.11 / §12.1 rule 6 — value column matches DataType
     TR_AttributeDefinition_DataTypeLock §4.7 / §12.1 rule 5 — DataType immutable once used
     TR_BOMComponent_CircularCheck      §6.5.2 / §12.1 rule 18 — no cycles, no self-reference
     TR_<table>_NoHardDelete            Chapter 8 — rows are never hard-deleted

   USER ATTRIBUTION: triggers read the acting user from SESSION_CONTEXT('UserId'),
   which the API sets once per request (see backend UserContextInterceptor). When it
   is absent — e.g. a direct SSMS edit — the trigger falls back to the row's own
   ModifiedBy/CreatedBy so the audit row is still written and still attributed.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

/* ==========================================================================
   §4.2 / §4.10 — denormalized ancestry must match ItemFamily's real ancestry.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_ItemMaster_ValidateAncestry', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_ItemMaster_ValidateAncestry;
GO
CREATE TRIGGER dbo.TR_ItemMaster_ValidateAncestry
ON dbo.ItemMaster
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF UPDATE(ItemFamilyId) OR UPDATE(ItemSubGroupId) OR UPDATE(ItemGroupId) OR UPDATE(ItemCategoryId)
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM inserted i
            JOIN dbo.ItemFamily   f  ON f.ItemFamilyId   = i.ItemFamilyId
            JOIN dbo.ItemSubGroup sg ON sg.ItemSubGroupId = f.ItemSubGroupId
            JOIN dbo.ItemGroup    g  ON g.ItemGroupId     = sg.ItemGroupId
            WHERE i.ItemSubGroupId <> f.ItemSubGroupId
               OR i.ItemGroupId    <> sg.ItemGroupId
               OR i.ItemCategoryId <> g.ItemCategoryId
        )
        BEGIN
            ROLLBACK TRANSACTION;
            THROW 51001, 'ITEM_ANCESTRY_MISMATCH: ItemCategoryId/ItemGroupId/ItemSubGroupId do not match the true ancestry of ItemFamilyId (SDS 4.2, 4.10).', 1;
        END
    END
END
GO

/* ==========================================================================
   §8.1 — item lifecycle state machine. No status is ever skipped.
     Draft            -> PendingApproval
     PendingApproval  -> Active | Draft (rejection returns the item to Draft)
     Active           -> PendingApproval (re-approval of an edit) | Inactive | Obsolete
     Inactive         -> Active | Obsolete
     Obsolete         -> (terminal)
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_ItemMaster_StatusTransition', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_ItemMaster_StatusTransition;
GO
CREATE TRIGGER dbo.TR_ItemMaster_StatusTransition
ON dbo.ItemMaster
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(ItemStatus) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON d.ItemId = i.ItemId
        WHERE i.ItemStatus <> d.ItemStatus
          AND NOT (
                (d.ItemStatus = 'Draft'           AND i.ItemStatus = 'PendingApproval')
             OR (d.ItemStatus = 'PendingApproval' AND i.ItemStatus IN ('Active','Draft'))
             OR (d.ItemStatus = 'Active'          AND i.ItemStatus IN ('PendingApproval','Inactive','Obsolete'))
             OR (d.ItemStatus = 'Inactive'        AND i.ItemStatus IN ('Active','Obsolete'))
          )
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51002, 'INVALID_STATUS_TRANSITION: requested ItemStatus change is not permitted from the current state (SDS 8.1).', 1;
    END

    /* §8.5 — Obsolete requires an ItemObsolescence row in the same transaction. */
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON d.ItemId = i.ItemId
        WHERE i.ItemStatus = 'Obsolete' AND d.ItemStatus <> 'Obsolete'
          AND NOT EXISTS (SELECT 1 FROM dbo.ItemObsolescence o
                          WHERE o.ItemId = i.ItemId AND o.IsDeleted = 0)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51003, 'OBSOLESCENCE_RECORD_REQUIRED: marking an item Obsolete requires a matching ItemObsolescence row in the same transaction (SDS 8.5).', 1;
    END
END
GO

/* ==========================================================================
   §8.3.1 — field-level, append-only audit of ItemMaster.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_ItemMaster_Audit', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_ItemMaster_Audit;
GO
CREATE TRIGGER dbo.TR_ItemMaster_Audit
ON dbo.ItemMaster
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @actor INT = TRY_CAST(SESSION_CONTEXT(N'UserId') AS INT);

    /* INSERT — one 'Insert' row recording the item's creation. */
    INSERT dbo.ItemAuditLog (ItemId, TableName, FieldName, OldValue, NewValue, ChangeType, ChangedBy, CreatedBy)
    SELECT i.ItemId, 'ItemMaster', 'ItemCode', NULL, i.ItemCode, 'Insert',
           COALESCE(@actor, i.CreatedBy), COALESCE(@actor, i.CreatedBy)
    FROM inserted i
    WHERE NOT EXISTS (SELECT 1 FROM deleted d WHERE d.ItemId = i.ItemId);

    /* UPDATE — one row per changed field. */
    ;WITH Changes AS (
        SELECT i.ItemId, COALESCE(@actor, i.ModifiedBy, i.CreatedBy) AS Actor,
               CASE WHEN i.IsDeleted = 1 AND d.IsDeleted = 0 THEN 'SoftDelete' ELSE 'Update' END AS ChangeType,
               v.FieldName, v.OldValue, v.NewValue
        FROM inserted i
        JOIN deleted  d ON d.ItemId = i.ItemId
        CROSS APPLY (VALUES
            ('ItemCode',            CONVERT(NVARCHAR(1000), d.ItemCode),            CONVERT(NVARCHAR(1000), i.ItemCode)),
            ('ItemName',            CONVERT(NVARCHAR(1000), d.ItemName),            CONVERT(NVARCHAR(1000), i.ItemName)),
            ('Description',         CONVERT(NVARCHAR(1000), d.Description),         CONVERT(NVARCHAR(1000), i.Description)),
            ('ItemCategoryId',      CONVERT(NVARCHAR(1000), d.ItemCategoryId),      CONVERT(NVARCHAR(1000), i.ItemCategoryId)),
            ('ItemGroupId',         CONVERT(NVARCHAR(1000), d.ItemGroupId),         CONVERT(NVARCHAR(1000), i.ItemGroupId)),
            ('ItemSubGroupId',      CONVERT(NVARCHAR(1000), d.ItemSubGroupId),      CONVERT(NVARCHAR(1000), i.ItemSubGroupId)),
            ('ItemFamilyId',        CONVERT(NVARCHAR(1000), d.ItemFamilyId),        CONVERT(NVARCHAR(1000), i.ItemFamilyId)),
            ('AttributeTemplateId', CONVERT(NVARCHAR(1000), d.AttributeTemplateId), CONVERT(NVARCHAR(1000), i.AttributeTemplateId)),
            ('BaseUOMId',           CONVERT(NVARCHAR(1000), d.BaseUOMId),           CONVERT(NVARCHAR(1000), i.BaseUOMId)),
            ('CanPurchase',         CONVERT(NVARCHAR(1000), d.CanPurchase),         CONVERT(NVARCHAR(1000), i.CanPurchase)),
            ('CanSell',             CONVERT(NVARCHAR(1000), d.CanSell),             CONVERT(NVARCHAR(1000), i.CanSell)),
            ('CanManufacture',      CONVERT(NVARCHAR(1000), d.CanManufacture),      CONVERT(NVARCHAR(1000), i.CanManufacture)),
            ('CanStock',            CONVERT(NVARCHAR(1000), d.CanStock),            CONVERT(NVARCHAR(1000), i.CanStock)),
            ('CanTransfer',         CONVERT(NVARCHAR(1000), d.CanTransfer),         CONVERT(NVARCHAR(1000), i.CanTransfer)),
            ('IsSerialControlled',  CONVERT(NVARCHAR(1000), d.IsSerialControlled),  CONVERT(NVARCHAR(1000), i.IsSerialControlled)),
            ('IsLotControlled',     CONVERT(NVARCHAR(1000), d.IsLotControlled),     CONVERT(NVARCHAR(1000), i.IsLotControlled)),
            ('ItemStatus',          CONVERT(NVARCHAR(1000), d.ItemStatus),          CONVERT(NVARCHAR(1000), i.ItemStatus)),
            ('VersionNumber',       CONVERT(NVARCHAR(1000), d.VersionNumber),       CONVERT(NVARCHAR(1000), i.VersionNumber)),
            ('IsActive',            CONVERT(NVARCHAR(1000), d.IsActive),            CONVERT(NVARCHAR(1000), i.IsActive)),
            ('IsDeleted',           CONVERT(NVARCHAR(1000), d.IsDeleted),           CONVERT(NVARCHAR(1000), i.IsDeleted))
        ) AS v(FieldName, OldValue, NewValue)
        WHERE EXISTS (SELECT v.OldValue EXCEPT SELECT v.NewValue)
    )
    INSERT dbo.ItemAuditLog (ItemId, TableName, FieldName, OldValue, NewValue, ChangeType, ChangedBy, CreatedBy)
    SELECT ItemId, 'ItemMaster', FieldName, OldValue, NewValue, ChangeType, Actor, Actor
    FROM Changes;
END
GO

/* ==========================================================================
   §8.3.1 — audit of ItemAttribute, keyed by AttributeCode as FieldName.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_ItemAttribute_Audit', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_ItemAttribute_Audit;
GO
CREATE TRIGGER dbo.TR_ItemAttribute_Audit
ON dbo.ItemAttribute
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @actor INT = TRY_CAST(SESSION_CONTEXT(N'UserId') AS INT);

    INSERT dbo.ItemAuditLog (ItemId, TableName, FieldName, OldValue, NewValue, ChangeType, ChangedBy, CreatedBy)
    SELECT i.ItemId,
           'ItemAttribute',
           ad.AttributeCode,
           CASE WHEN d.ItemAttributeId IS NULL THEN NULL
                ELSE COALESCE(d.ValueText, CONVERT(NVARCHAR(1000), d.ValueNumber),
                              CONVERT(NVARCHAR(1000), d.ValueDate), CONVERT(NVARCHAR(1000), d.ValueBoolean)) END,
           COALESCE(i.ValueText, CONVERT(NVARCHAR(1000), i.ValueNumber),
                    CONVERT(NVARCHAR(1000), i.ValueDate), CONVERT(NVARCHAR(1000), i.ValueBoolean)),
           CASE WHEN d.ItemAttributeId IS NULL THEN 'Insert'
                WHEN i.IsDeleted = 1 AND d.IsDeleted = 0 THEN 'SoftDelete'
                ELSE 'Update' END,
           COALESCE(@actor, i.ModifiedBy, i.CreatedBy),
           COALESCE(@actor, i.ModifiedBy, i.CreatedBy)
    FROM inserted i
    LEFT JOIN deleted d ON d.ItemAttributeId = i.ItemAttributeId
    JOIN dbo.AttributeDefinition ad ON ad.AttributeDefinitionId = i.AttributeDefinitionId
    WHERE d.ItemAttributeId IS NULL
       OR EXISTS (
            SELECT d.ValueText, d.ValueNumber, d.ValueDate, d.ValueBoolean, d.IsDeleted, d.IsActive
            EXCEPT
            SELECT i.ValueText, i.ValueNumber, i.ValueDate, i.ValueBoolean, i.IsDeleted, i.IsActive
          );
END
GO

/* ==========================================================================
   §4.11 / §12.1 rule 6 — the populated value column must match the referenced
   AttributeDefinition.DataType. Cross-table, so it cannot be a CHECK.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_ItemAttribute_ValueDataType', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_ItemAttribute_ValueDataType;
GO
CREATE TRIGGER dbo.TR_ItemAttribute_ValueDataType
ON dbo.ItemAttribute
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.AttributeDefinition ad ON ad.AttributeDefinitionId = i.AttributeDefinitionId
        WHERE NOT (
                (ad.DataType IN ('Text','Enum') AND i.ValueText    IS NOT NULL)
             OR (ad.DataType = 'Number'         AND i.ValueNumber  IS NOT NULL)
             OR (ad.DataType = 'Date'           AND i.ValueDate    IS NOT NULL)
             OR (ad.DataType = 'Boolean'        AND i.ValueBoolean IS NOT NULL)
        )
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51004, 'ATTRIBUTE_DATATYPE_MISMATCH: the populated value column does not match AttributeDefinition.DataType (SDS 4.11).', 1;
    END

    /* Enum values must be one of AttributeDefinition.EnumOptions (JSON array). */
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.AttributeDefinition ad ON ad.AttributeDefinitionId = i.AttributeDefinitionId
        WHERE ad.DataType = 'Enum'
          AND ISJSON(ad.EnumOptions) = 1
          AND NOT EXISTS (SELECT 1 FROM OPENJSON(ad.EnumOptions) o WHERE o.value = i.ValueText)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51005, 'ATTRIBUTE_DATATYPE_MISMATCH: value is not one of the allowed EnumOptions (SDS 4.7).', 1;
    END
END
GO

/* ==========================================================================
   §4.7 / §12.1 rule 5 — DataType is immutable once any ItemAttribute uses it.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_AttributeDefinition_DataTypeLock', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_AttributeDefinition_DataTypeLock;
GO
CREATE TRIGGER dbo.TR_AttributeDefinition_DataTypeLock
ON dbo.AttributeDefinition
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT UPDATE(DataType) RETURN;

    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN deleted  d ON d.AttributeDefinitionId = i.AttributeDefinitionId
        WHERE i.DataType <> d.DataType
          AND EXISTS (SELECT 1 FROM dbo.ItemAttribute ia
                      WHERE ia.AttributeDefinitionId = i.AttributeDefinitionId)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51006, 'ATTRIBUTE_DATATYPE_LOCKED: DataType cannot change once any ItemAttribute references this definition — create a new AttributeDefinition and migrate (SDS 4.7, 9.5.3).', 1;
    END
END
GO

/* ==========================================================================
   §6.5.2 / §12.1 rule 18 — no self-reference, no circular BOM structure.
   ========================================================================== */
IF OBJECT_ID(N'dbo.TR_BOMComponent_CircularCheck', N'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_BOMComponent_CircularCheck;
GO
CREATE TRIGGER dbo.TR_BOMComponent_CircularCheck
ON dbo.BOMComponent
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    /* Direct self-reference: component = its own BOM's parent. */
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.BillOfMaterial b ON b.BOMId = i.BOMId
        WHERE b.ParentItemId = i.ComponentItemId
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51007, 'BOM_CIRCULAR_REFERENCE: a component cannot be its own BOM parent (SDS 6.5.2).', 1;
    END

    /* Indirect cycle: walk the component's own BOM tree looking for the root parent. */
    DECLARE @cycleFound BIT = 0;

    ;WITH Descend AS (
        SELECT b.ParentItemId AS RootParent, i.ComponentItemId AS NodeItemId, 0 AS Lvl
        FROM inserted i
        JOIN dbo.BillOfMaterial b ON b.BOMId = i.BOMId
        UNION ALL
        SELECT dd.RootParent, c.ComponentItemId, dd.Lvl + 1
        FROM Descend dd
        JOIN dbo.BillOfMaterial b2 ON b2.ParentItemId = dd.NodeItemId
                                  AND b2.IsDeleted = 0 AND b2.Status <> 'Obsolete'
        JOIN dbo.BOMComponent  c  ON c.BOMId = b2.BOMId AND c.IsDeleted = 0
        WHERE dd.Lvl < 50
    )
    SELECT TOP (1) @cycleFound = 1
    FROM Descend
    WHERE NodeItemId = RootParent AND Lvl > 0
    OPTION (MAXRECURSION 0);

    IF @cycleFound = 1
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 51008, 'BOM_CIRCULAR_REFERENCE: the proposed BOM structure would create a cycle (SDS 6.5.2, 9.1.2).', 1;
    END
END
GO

/* ==========================================================================
   Chapter 8 — rows are never hard-deleted. INSTEAD OF DELETE triggers turn a
   DELETE into the soft-delete the specification requires.
   ========================================================================== */
DECLARE @t SYSNAME, @pk SYSNAME, @sql NVARCHAR(MAX);

DECLARE softDelete CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName, PkColumn
    FROM (VALUES
        ('ItemMaster',          'ItemId'),
        ('ItemAttribute',       'ItemAttributeId'),
        ('ItemCategory',        'ItemCategoryId'),
        ('ItemGroup',           'ItemGroupId'),
        ('ItemSubGroup',        'ItemSubGroupId'),
        ('ItemFamily',          'ItemFamilyId'),
        ('AttributeDefinition', 'AttributeDefinitionId'),
        ('AttributeTemplate',   'AttributeTemplateId'),
        ('TemplateAttribute',   'TemplateAttributeId'),
        ('BusinessUnitItem',    'BusinessUnitItemId'),
        ('Company',             'CompanyId'),
        ('BusinessUnit',        'BusinessUnitId'),
        ('UOM',                 'UOMId'),
        ('ItemPackaging',       'ItemPackagingId')
    ) AS m(TableName, PkColumn);

OPEN softDelete;
FETCH NEXT FROM softDelete INTO @t, @pk;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @sql = N'IF OBJECT_ID(N''dbo.TR_' + @t + N'_NoHardDelete'', N''TR'') IS NOT NULL'
             + N' DROP TRIGGER dbo.TR_' + @t + N'_NoHardDelete;';
    EXEC sp_executesql @sql;

    SET @sql = N'
CREATE TRIGGER dbo.TR_' + @t + N'_NoHardDelete
ON dbo.' + @t + N'
INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE t
       SET t.IsDeleted    = 1,
           t.IsActive     = 0,
           t.ModifiedBy   = COALESCE(TRY_CAST(SESSION_CONTEXT(N''UserId'') AS INT), t.ModifiedBy, t.CreatedBy),
           t.ModifiedDate = SYSUTCDATETIME()
      FROM dbo.' + @t + N' AS t
      JOIN deleted AS d ON d.' + @pk + N' = t.' + @pk + N';
END';
    EXEC sp_executesql @sql;

    FETCH NEXT FROM softDelete INTO @t, @pk;
END
CLOSE softDelete;
DEALLOCATE softDelete;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigration WHERE ScriptName = '10_triggers.sql')
    INSERT dbo.SchemaMigration (ScriptName) VALUES ('10_triggers.sql');
GO
