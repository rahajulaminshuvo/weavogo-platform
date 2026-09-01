# Traceability — SDS to implementation

Every table, validation rule, error code and security row in the specification, mapped to
the file that implements it. Use this as the QA checklist.

## Chapter 4 — Core schema

| SDS | Table | File |
|---|---|---|
| §4.1 | Standard audit columns (7, on every table) | all `0*_schema_*.sql`; FKs generated in `09_indexes_and_audit_fks.sql` |
| §4.3 | `ItemCategory` | `04_schema_classification.sql` |
| §4.4 | `ItemGroup` | `04_schema_classification.sql` |
| §4.5 | `ItemSubGroup` | `04_schema_classification.sql` |
| §4.6 | `ItemFamily` | `04_schema_classification.sql` |
| §4.7 | `AttributeDefinition` | `04_schema_classification.sql` |
| §4.8 | `AttributeTemplate` | `04_schema_classification.sql` |
| §4.9 | `TemplateAttribute` | `04_schema_classification.sql` |
| §4.10 | `ItemMaster` | `05_schema_item.sql` |
| §4.11 | `ItemAttribute` | `05_schema_item.sql` |

## Chapter 5 — Supporting tables

| SDS | Table | File |
|---|---|---|
| §5.2.1 | `Company` | `02_schema_organization.sql` |
| §5.2.2 | `BusinessUnit` | `02_schema_organization.sql` |
| §5.3 | `BusinessUnitItem` | `05_schema_item.sql` |
| §5.4 | `UOM` | `03_schema_uom.sql` |
| §5.5 | `UOMConversion` | `03_schema_uom.sql` |
| §5.6 | `ItemPackaging` | `05_schema_item.sql` |

## Chapter 6 — Functional mapping

`06_schema_functional.sql`: `Warehouse` (§6.1.1), `WarehouseItem` (§6.1.2),
`InventoryPolicy` (§6.1.3), `Supplier` (§6.2.1), `SupplierItem` (§6.2.2),
`SupplierPrice` (§6.2.3), `Customer`/`CustomerItem`/`SalesPrice` (§6.3.1),
`GLMapping` (§6.4.1), `TaxMapping` (§6.4.2), `BillOfMaterial` (§6.5.1),
`BOMComponent` (§6.5.2), `AssetMaster` (§6.6.1).

## Chapter 7 — Quality, traceability, identification

`07_schema_quality.sql`: `QCParameterTemplate` (§7.1.1), `QCParameter` (§7.1.2),
`ItemQCProfile` (§7.1.3), `ItemLot` (§7.2.1), `ItemSerial` (§7.2.2),
`ItemBarcode` (§7.3.1), `ItemRFID` (§7.3.2), `ItemDocument` (§7.4.1).

## Chapter 8 — Governance

`08_schema_governance.sql`: `ApprovalWorkflowTemplate` (§8.2.1), `ApprovalStep` (§8.2.2),
`ItemApprovalRequest` (§8.2.3), `ItemApprovalAction` (§8.2.4), `ItemAuditLog` (§8.3.1),
`ItemVersion` (§8.4.1), `ItemObsolescence` (§8.5.1).
State machine: `10_triggers.sql` `TR_ItemMaster_StatusTransition` +
`Domain/Items.cs` `ItemStatuses.CanTransition`.

## Chapter 10 — API

| SDS | Endpoint | File |
|---|---|---|
| §10.3 | `POST /items` | `Controllers/ItemsController.Create` → `ItemService.CreateAsync` |
| §10.4 | `GET /items/{id}` | `ItemsController.GetById` → `ItemService.GetAsync` |
| §10.5 | `GET /items` | `ItemsController.Search` → `ItemService.SearchAsync` |
| §10.6 | `PATCH /items/{id}` | `ItemsController.Patch` → `ItemService.PatchAsync` / `OpenReapprovalAsync` |
| §10.7 | `POST /items/{id}/submit` | `ItemsController.Submit` → `ApprovalService.SubmitAsync` |
| §10.7 | `POST /approval-requests/{id}/approval-actions` | `ApprovalRequestsController.Act` → `ApprovalService.RecordActionAsync` |
| §10.8 | Error envelope | `Common/ApiError.cs`, `Middleware/ErrorHandlingMiddleware.cs` |
| §10.9 | T-SQL DDL | reproduced verbatim in `04_` and `05_` |

## Chapter 12.1 — Validation matrix

| # | Rule | Enforced by |
|---|---|---|
| 1 | `CategoryCode` 2–10 chars, uppercase A–Z/0–9 | `CK_ItemCategory_CodeFormat` |
| 2 | `ItemCode` 3–30 chars, unique, immutable post-transaction | `CK_ItemMaster_CodeFormat`, `UQ_ItemMaster_Code`, `ItemService.PatchAsync` → `ITEM_CODE_LOCKED` |
| 3 | One of `CanPurchase`/`CanSell`/`CanManufacture` | `CK_ItemMaster_TransactableFlags` + `ItemService.ValidateFlags` |
| 4 | Status transitions follow the state machine | `TR_ItemMaster_StatusTransition` + `ItemStatuses.CanTransition` |
| 5 | `AttributeDefinition.DataType` immutable once used | `TR_AttributeDefinition_DataTypeLock` |
| 6 | Exactly one value column, matching the DataType | `CK_ItemAttribute_SingleValue` + `TR_ItemAttribute_ValueDataType` |
| 7 | Required template attributes before Active | `AttributeResolver.AssertRequiredAttributesPresentAsync` |
| 8, 9 | Lot QC status / expiry gate issuance | `CK_ItemLot_QCStatus`, `IX_ItemLot_ItemStatus` (transaction layer, out of scope per §1.2) |
| 10 | `SerialNumber` unique per item | `UQ_ItemSerial_ItemSerialNumber` |
| 11 | `BarcodeValue` globally unique | `UQ_ItemBarcode_Value` |
| 12 | `EPCCode` globally unique | `UQ_ItemRFID_EPCCode` |
| 13 | `BusinessUnit.CompanyId` FK, non-recursive | `FK_BusinessUnit_Company` |
| 14 | `BusinessUnitItem` unique per (item, unit) | `UQ_BusinessUnitItem_ItemUnit` + `ITEM_NOT_AUTHORIZED_FOR_ENTITY` |
| 15 | `GLMapping` exactly one target | `CK_GLMapping_OneTarget` |
| 16 | `TaxMapping` unique per (target, jurisdiction) | `UX_TaxMapping_CategoryJurisdiction`, `UX_TaxMapping_ItemJurisdiction` |
| 17 | One Active BOM version per parent | `UX_BOM_OneActivePerParent` |
| 18 | No self-reference, no circular BOM | `TR_BOMComponent_CircularCheck` |
| 19 | `AssetCode` globally unique | `UQ_AssetMaster_Code` |
| 20 | Obsolete requires an `ItemObsolescence` row | `TR_ItemMaster_StatusTransition` |
| 21 | Any change to an Active item creates a version row | `ApprovalService.CompleteApprovalAsync` + `TR_ItemMaster_Audit` |
| 22 | Approver must hold the step's role | `ApprovalService.RecordActionAsync` → `APPROVAL_ROLE_MISMATCH` |
| 23 | At most one preferred supplier per item | `UX_SupplierItem_OnePreferredPerItem` |
| 24 | `QuantityOnHand` never edited directly | no API write path exists; `WarehouseItem` is read-only in the context |

## Chapter 12.2 — Error catalogue

All fifteen codes live in `Common/ApiError.cs`; the status-code mapping is asserted by
`tests/WeavoGo.ItemMaster.Tests/ErrorCatalogueTests.cs`. Trigger `THROW` numbers
51001–51008 and SQL error numbers 2601/2627/547 are translated in
`Middleware/ErrorHandlingMiddleware.TryTranslate`.

## Chapter 12.3 — Security matrix

Policies are declared in `Program.cs` (one per matrix row) and enforced with
`[Authorize(Policy = …)]` on each action. "Scoped" rows are additionally checked against
`UserBusinessUnit` inside `ItemService.AssertReadScope` / `AssertWriteScope`, because a
policy alone cannot see which item the request targets. System Administrator is unscoped
by design, per the §12.3 closing note.

## Verification

| Layer | Command | Covers |
|---|---|---|
| SQL structure | `python3 database/tools/lint_ddl.py` | FK targets, ordering, audit columns, seed alignment |
| SQL behaviour | `database/99_verify.sql` | 18 checks incl. ancestry drift, illegal transition, circular BOM, soft delete |
| API units | `dotnet test` | State machine, error catalogue, envelope shape |
| API end-to-end | `newman run …` | Chapter 10 contracts, Chapter 8 workflow, Chapter 12 matrix |
