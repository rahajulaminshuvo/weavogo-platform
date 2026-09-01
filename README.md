# WeavoGo — Universal Item Master

Implementation of the 13-chapter *Universal Item Master* SDS: SQL Server schema (Phase 1)
and an ASP.NET Core 8 + EF Core REST API (Phase 2).

```
database/                SQL Server DDL, triggers, seed data, migration runner, verifier
backend/
  src/WeavoGo.ItemMaster.Api/     ASP.NET Core 8 Web API (EF Core, JWT, Chapter 12 RBAC)
  tests/WeavoGo.ItemMaster.Tests/ xUnit unit tests
  tests/postman/                  Newman/Postman integration collection (28 requests)
docs/                    Traceability matrix and implementation notes
```

---

## Phase 1 — Database

48 tables, 224 named constraints, 7 governance triggers, 14 soft-delete triggers,
full reference data and the SDS's worked examples as seed rows.

### Run it

```bash
# 1. SQL Server (Docker)
docker run -d --name sqlserver -p 1433:1433 \
  -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Your_password123" \
  mcr.microsoft.com/mssql/server:2022-latest

# 2. Apply every migration, then verify
cd database
SA_PASSWORD='Your_password123' ./run-migrations.sh
```

Alternatives:

- `sqlcmd -S localhost -U sa -P '…' -C -b -i migrate.sql` (SQLCMD mode; SSMS: *Query → SQLCMD Mode*)
- `./run-migrations.sh --no-sample-data` — schema + reference data only
- `./run-migrations.sh --verify-only` — re-run `99_verify.sql` against an existing database

### Script order

| Script | Contents | SDS |
|---|---|---|
| `00_create_database.sql` | Database + `SchemaMigration` bookkeeping | — |
| `01_schema_platform.sql` | `UserAccount`, `Role`, `UserRole` | §4.1 FK target, Ch. 12 |
| `02_schema_organization.sql` | `Company`, `BusinessUnit`, `UserBusinessUnit` | §5.2 |
| `03_schema_uom.sql` | `UOM`, `UOMConversion` | §5.4–5.5 |
| `04_schema_classification.sql` | Category → Group → Sub-Group → Family, attribute engine | §4.3–4.9 |
| `05_schema_item.sql` | `ItemMaster`, `ItemAttribute`, `BusinessUnitItem`, `ItemPackaging` | §4.10–4.11, §5.3, §5.6 |
| `06_schema_functional.sql` | Inventory, procurement, sales, GL/tax, BOM, assets | Ch. 6 |
| `07_schema_quality.sql` | QC templates/parameters/profiles, lot, serial, barcode, RFID, documents | Ch. 7 |
| `08_schema_governance.sql` | Approval workflow, audit log, versions, obsolescence | Ch. 8 |
| `09_indexes_and_audit_fks.sql` | Query indexes + the §4.1 `CreatedBy`/`ModifiedBy` FKs on every table | §4.1, §10.5 |
| `10_triggers.sql` | Ancestry sync, state machine, audit, datatype lock, circular BOM, no-hard-delete | §4.2, §4.7, §6.5.2, §8.1, §8.3 |
| `11_seed_reference.sql` | Categories, groups, families, attribute dictionary, templates, UOM, units, warehouses, suppliers, QC, workflows | Ch. 3–8 |
| `12_seed_sample_data.sql` | 18 worked-example items with attributes, mappings, lots, serials, approvals | Ch. 4–8 |
| `99_verify.sql` | 18 PASS/FAIL checks including four negative tests | Ch. 12 |

Every script is idempotent and records itself in `dbo.SchemaMigration`.

### What the triggers enforce

| Trigger | Rule | SDS |
|---|---|---|
| `TR_ItemMaster_ValidateAncestry` | Denormalized category/group/sub-group must match the family's real ancestry | §4.2 |
| `TR_ItemMaster_StatusTransition` | No skipped lifecycle states; Obsolete needs an `ItemObsolescence` row | §8.1, §8.5 |
| `TR_ItemMaster_Audit` / `TR_ItemAttribute_Audit` | Field-level append-only audit, written by the database not the app | §8.3.1 |
| `TR_ItemAttribute_ValueDataType` | Populated value column matches `AttributeDefinition.DataType`; enum values are members | §4.11, §4.7 |
| `TR_AttributeDefinition_DataTypeLock` | `DataType` immutable once any `ItemAttribute` references it | §4.7, §9.5.3 |
| `TR_BOMComponent_CircularCheck` | No self-reference, no cycles (recursive walk, depth-capped) | §6.5.2, §9.1.2 |
| `TR_<table>_NoHardDelete` (14) | `DELETE` becomes `IsDeleted = 1, IsActive = 0` | Ch. 8 |

### Static lint (no server needed)

```bash
python3 database/tools/lint_ddl.py
```

Checks parenthesis balance, duplicate tables, FK target table/column existence, FK
declaration order against the migration order, globally unique constraint names, the
§4.1 audit footprint on every table, and seed `INSERT` column/value alignment.

---

## Phase 2 — API

ASP.NET Core 8 + EF Core 8, database-first against the schema above. JWT bearer auth with
the Chapter 12 roles as claims; business-unit scoping applied in the service layer.

### Run it

```bash
cd backend
dotnet restore                 # requires nuget.org access — see "Known limitation" below
dotnet build
dotnet run --project src/WeavoGo.ItemMaster.Api --urls http://localhost:5080
```

Swagger UI: <http://localhost:5080/swagger>. Health: `GET /health`.

Set the connection string and a real signing key before anything but local use:

```bash
export ConnectionStrings__ItemMaster="Server=localhost,1433;Database=WeavoItemMaster;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
export Jwt__SigningKey="a-32-byte-or-longer-random-secret"
```

### Endpoints (SDS §10.2)

| Method & path | Purpose | SDS |
|---|---|---|
| `POST /api/v1/auth/login` | Issue the bearer token | §10.1 |
| `POST /api/v1/items` | Create a Draft item with attributes and unit authorizations | §10.3 |
| `GET /api/v1/items/{itemId}` | Full detail: ancestry, resolved attributes, units, packaging, QC profile | §10.4 |
| `GET /api/v1/items` | Search — `categoryCode`, `status`, `businessEntityId`, `search`, `page`, `pageSize` (max 100) | §10.5 |
| `PATCH /api/v1/items/{itemId}` | Partial update; on an Active item this opens a re-approval | §10.6 |
| `POST /api/v1/items/{itemId}/submit` | Submit into the approval workflow | §10.7 |
| `POST /api/v1/approval-requests/{id}/approval-actions` | Record an approve/reject decision | §10.7 |
| `POST /api/v1/items/{itemId}/approval-actions` | Same decision, addressed by item | §10.2 |
| `GET /api/v1/items/{itemId}/versions` | Version history with snapshots | §8.4 |
| `POST /api/v1/items/{itemId}/business-units` | Authorize / revoke business units | §5.3 |
| `GET /api/v1/items/{itemId}/audit-log` | Field-level audit trail | §8.3 |
| `GET /api/v1/categories`, `…/groups`, `…/sub-groups`, `…/families`, `/attribute-templates/{id}`, `/uoms`, `/business-units`, `/workflow-templates` | Reference data for the Chapter 11 screens | §11.2 |

Every non-2xx response uses the §10.8 envelope:

```json
{ "error": { "code": "ITEM_NOT_AUTHORIZED_FOR_ENTITY",
             "message": "Item 6 is not authorized for business unit 13.",
             "field": null, "traceId": "…" } }
```

All fifteen §12.2 error codes are implemented, and the database's own trigger `THROW`
numbers (51001–51008) and unique-index violations are translated into the same codes.

### Test credentials

Seeded by `11_seed_reference.sql`. **Password for every account: `Weavo#2026`.**

| User name | Role | Scope |
|---|---|---|
| `system` | System Administrator | unscoped |
| `catmgr` | Category Manager | units 1, 6, 7, 10, 13 |
| `finctrl` | Finance Controller | unscoped |
| `qcmgr` | QC Manager | unscoped |
| `itsec` | IT Security Review | unscoped |
| `whclerk` | Warehouse Clerk | unit 6 |
| `salesuser` | Sales User | unit 1 |
| `readonly` | Read-Only | unscoped |

Change them before any deployment that is not a local sandbox.

### Tests

```bash
cd backend
dotnet test                                     # unit tests

cd tests/postman
python3 build_collection.py                     # regenerate if you edit the script
newman run ItemMaster.postman_collection.json -e local.postman_environment.json
```

The Newman collection walks the whole lifecycle end to end: create a Draft, get
`ATTRIBUTE_REQUIRED_MISSING` on premature submit, fill the attributes, submit, walk all
three ICT-ASSET approval steps (including a deliberate `APPROVAL_ROLE_MISMATCH`), confirm
the item goes Active, PATCH the Active item into a re-approval, approve to version 2, and
assert the audit log and version history were populated by the database triggers. It also
covers the §12.3 matrix (Read-Only can view but not create) and the 401 path.

---

## Known limitation of this build

The API source is complete but **was not compiled in the environment it was generated in**:
that sandbox has no outbound access to `nuget.org`, so `dotnet restore` could not fetch
EF Core, the JWT bearer package or Swashbuckle, and no SQL Server instance was reachable
to execute the SQL. Run `dotnet build` and `database/run-migrations.sh` on a machine with
normal network access first; expect to fix ordinary compile-time slips rather than design
problems. The SQL was checked with the static linter above, which passes.

---

## Deliberate deviations from the SDS

Both are marked in the code where they occur.

1. **Platform tables.** The SDS points `CreatedBy`/`ModifiedBy` at `UserAccount.UserId` and
   specifies a role matrix, but never lays out those tables. `01_schema_platform.sql` and
   `UserBusinessUnit` define the minimum surface those references require.
2. **Classification rows with Ids 11+.** Chapters 6–8 name a T-shirt, Paracetamol, an MRI
   scanner, a forklift, a generator and an internet subscription, but the Chapter 4
   reference dataset stops at ten families. `12_seed_sample_data.sql` adds groups,
   sub-groups and families so those examples have valid foreign keys — rows, not schema.

One judgement call: `RACKUNIT` is seeded as an `Enum` attribute (`1U`/`2U`/`4U`/`Tower`)
rather than free text, so the enum path in `AttributeDefinition.EnumOptions` is exercised
by real data. The SDS gives `2U` as an example value but does not fix the data type.

---

## Next: Phase 3

Angular 21 + Tailwind 4 + PrimeNG UI for the three Chapter 11 screens — Item Creation Form
(cascading dropdowns, dynamic attributes), Item Detail & Approval, Item Search & List. The
reference endpoints listed above exist specifically to back those screens.
