/* =============================================================================
   Universal Item Master — 99_verify.sql
   Post-migration self-check. Prints one PASS/FAIL line per evaluation criterion
   and raises an error at the end if anything failed, so CI can gate on it.
   Read-only apart from the two negative tests, which run inside a rolled-back
   transaction and leave no trace.
   ============================================================================= */
USE WeavoItemMaster;
GO
SET NOCOUNT ON;
GO

DECLARE @failures INT = 0;

DECLARE @results TABLE (Check_ VARCHAR(120), Expected VARCHAR(60), Actual VARCHAR(60), Result CHAR(4));

/* ---- 1. Table inventory -------------------------------------------------- */
INSERT @results
SELECT 'Tables created (>= 40)', '>=40', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) >= 40 THEN 'PASS' ELSE 'FAIL' END
FROM sys.tables WHERE name <> 'SchemaMigration';

/* ---- 2. Every table carries the §4.1 audit footprint --------------------- */
INSERT @results
SELECT 'Tables missing audit columns', '0', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END
FROM sys.tables t
WHERE t.name <> 'SchemaMigration'
  AND (SELECT COUNT(*) FROM sys.columns c
       WHERE c.object_id = t.object_id
         AND c.name IN ('CreatedBy','CreatedDate','ModifiedBy','ModifiedDate','IsActive','IsDeleted','RowVersion')) < 7;

/* ---- 3. Required constraints -------------------------------------------- */
INSERT @results
SELECT 'Named constraints present', '>=90', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) >= 90 THEN 'PASS' ELSE 'FAIL' END
FROM (
    SELECT name FROM sys.key_constraints
    UNION ALL SELECT name FROM sys.foreign_keys
    UNION ALL SELECT name FROM sys.check_constraints
) c;

/* ---- 4. Triggers -------------------------------------------------------- */
INSERT @results
SELECT 'Governance triggers present', '7', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) = 7 THEN 'PASS' ELSE 'FAIL' END
FROM sys.triggers
WHERE name IN ('TR_ItemMaster_ValidateAncestry','TR_ItemMaster_StatusTransition','TR_ItemMaster_Audit',
               'TR_ItemAttribute_Audit','TR_ItemAttribute_ValueDataType','TR_AttributeDefinition_DataTypeLock',
               'TR_BOMComponent_CircularCheck');

INSERT @results
SELECT 'Soft-delete triggers present', '14', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) = 14 THEN 'PASS' ELSE 'FAIL' END
FROM sys.triggers WHERE name LIKE 'TR!_%!_NoHardDelete' ESCAPE '!';

/* ---- 5. Reference data -------------------------------------------------- */
INSERT @results SELECT 'ItemCategory rows',   '10', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) = 10 THEN 'PASS' ELSE 'FAIL' END FROM dbo.ItemCategory;
INSERT @results SELECT 'ItemFamily rows',     '>=10', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) >= 10 THEN 'PASS' ELSE 'FAIL' END FROM dbo.ItemFamily;
INSERT @results SELECT 'UOM rows',            '10', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) = 10 THEN 'PASS' ELSE 'FAIL' END FROM dbo.UOM;
INSERT @results SELECT 'BusinessUnit rows',   '19', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) = 19 THEN 'PASS' ELSE 'FAIL' END FROM dbo.BusinessUnit;
INSERT @results SELECT 'AttributeTemplate rows','10', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) = 10 THEN 'PASS' ELSE 'FAIL' END FROM dbo.AttributeTemplate;
INSERT @results SELECT 'ApprovalStep rows',   '10', CAST(COUNT(*) AS VARCHAR(20)), CASE WHEN COUNT(*) = 10 THEN 'PASS' ELSE 'FAIL' END FROM dbo.ApprovalStep;

/* ---- 6. Denormalized ancestry is consistent (§4.2) ---------------------- */
INSERT @results
SELECT 'ItemMaster ancestry drift', '0', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END
FROM dbo.ItemMaster i
JOIN dbo.ItemFamily   f  ON f.ItemFamilyId    = i.ItemFamilyId
JOIN dbo.ItemSubGroup sg ON sg.ItemSubGroupId = f.ItemSubGroupId
JOIN dbo.ItemGroup    g  ON g.ItemGroupId     = sg.ItemGroupId
WHERE i.ItemSubGroupId <> f.ItemSubGroupId
   OR i.ItemGroupId    <> sg.ItemGroupId
   OR i.ItemCategoryId <> g.ItemCategoryId;

/* ---- 7. Attribute values match their definition's DataType (§4.11) ------ */
INSERT @results
SELECT 'ItemAttribute DataType mismatches', '0', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) = 0 THEN 'PASS' ELSE 'FAIL' END
FROM dbo.ItemAttribute ia
JOIN dbo.AttributeDefinition ad ON ad.AttributeDefinitionId = ia.AttributeDefinitionId
WHERE NOT (
        (ad.DataType IN ('Text','Enum') AND ia.ValueText    IS NOT NULL)
     OR (ad.DataType = 'Number'         AND ia.ValueNumber  IS NOT NULL)
     OR (ad.DataType = 'Date'           AND ia.ValueDate    IS NOT NULL)
     OR (ad.DataType = 'Boolean'        AND ia.ValueBoolean IS NOT NULL));

/* ---- 8. Audit log was populated by the triggers (§8.3) ------------------ */
INSERT @results
SELECT 'ItemAuditLog populated', '>0', CAST(COUNT(*) AS VARCHAR(20)),
       CASE WHEN COUNT(*) > 0 THEN 'PASS' ELSE 'FAIL' END
FROM dbo.ItemAuditLog;

/* ---- 9. Negative test: ancestry drift is rejected ----------------------- */
BEGIN TRAN;
BEGIN TRY
    UPDATE dbo.ItemMaster SET ItemCategoryId = 1 WHERE ItemId = 6;   -- Dell laptop is ICT (6)
    INSERT @results VALUES ('Ancestry drift rejected', 'error', 'accepted', 'FAIL');
END TRY
BEGIN CATCH
    INSERT @results VALUES ('Ancestry drift rejected', 'error', ERROR_NUMBER(), 'PASS');
END CATCH
IF @@TRANCOUNT > 0 ROLLBACK TRAN;

/* ---- 10. Negative test: illegal status transition is rejected ----------- */
BEGIN TRAN;
BEGIN TRY
    UPDATE dbo.ItemMaster SET ItemStatus = 'Active' WHERE ItemId = 18;  -- Obsolete is terminal
    INSERT @results VALUES ('Illegal status transition rejected', 'error', 'accepted', 'FAIL');
END TRY
BEGIN CATCH
    INSERT @results VALUES ('Illegal status transition rejected', 'error', ERROR_NUMBER(), 'PASS');
END CATCH
IF @@TRANCOUNT > 0 ROLLBACK TRAN;

/* ---- 11. Negative test: circular BOM is rejected ------------------------ */
BEGIN TRAN;
BEGIN TRY
    INSERT dbo.BillOfMaterial (ParentItemId, BOMVersion, EffectiveDate, Status, CreatedBy)
    VALUES (1, 1, '2026-01-01', 'Active', 1);                       -- BOM for the fabric
    INSERT dbo.BOMComponent (BOMId, ComponentItemId, QtyPerUnit, UOMId, CreatedBy)
    VALUES (SCOPE_IDENTITY(), 11, 1, 1, 1);                         -- fabric contains the T-shirt -> cycle
    INSERT @results VALUES ('Circular BOM rejected', 'error', 'accepted', 'FAIL');
END TRY
BEGIN CATCH
    INSERT @results VALUES ('Circular BOM rejected', 'error', ERROR_NUMBER(), 'PASS');
END CATCH
IF @@TRANCOUNT > 0 ROLLBACK TRAN;

/* ---- 12. Negative test: hard delete becomes a soft delete --------------- */
BEGIN TRAN;
DELETE FROM dbo.ItemMaster WHERE ItemId = 10;
INSERT @results
SELECT 'DELETE converted to soft delete', '1/1', CAST(COUNT(*) AS VARCHAR(20)) + '/1',
       CASE WHEN COUNT(*) = 1 THEN 'PASS' ELSE 'FAIL' END
FROM dbo.ItemMaster WHERE ItemId = 10 AND IsDeleted = 1;
ROLLBACK TRAN;

/* ---- Report ------------------------------------------------------------- */
SELECT Result, Check_, Expected, Actual FROM @results ORDER BY CASE WHEN Result = 'FAIL' THEN 0 ELSE 1 END, Check_;

SELECT @failures = COUNT(*) FROM @results WHERE Result = 'FAIL';

IF @failures > 0
BEGIN
    DECLARE @msg NVARCHAR(200) = CONCAT('Verification failed: ', @failures, ' check(s) did not pass.');
    THROW 52000, @msg, 1;
END
ELSE
    PRINT 'Verification passed — all checks OK.';
GO
