# WeavoGo_Platform — Implementation Status

Snapshot of `backend/` as built, measured against the target architecture in
`weavo-go.txt`.

**Date:** 2026-09-06
**Build:** `dotnet build WeavoGo_Platform.sln` → **0 warnings, 0 errors**
**Scope:** 7 projects in the solution, 31 C# files, 1 service vertical complete.

---

## 1. Headline numbers

| Metric | Value |
|---|---|
| Projects in solution | **7** of ~120 planned |
| Service verticals complete | **1** (ItemMaster) of 29 |
| BuildingBlocks libraries | **3** of 6 |
| Empty scaffolded directories | **126** |
| C# source files | 31 (incl. 5 EF migration files) |
| EF migrations applied to `WeavoMasterDB` | 2 |
| Overall completion | **~6%** by project count |

The skeleton is fully scaffolded; the flesh exists on one bone. That is a
reasonable state — ItemMaster is the reference implementation the remaining 28
services are meant to be cloned from.

---

## 2. What is actually built

### 2.1 BuildingBlocks (3 of 6)

| Project | Status | Contents |
|---|---|---|
| `Weavo.BuildingBlocks.Kernel` | **Built** | `Entity<TId>`, `AggregateRoot<TId>`, `IDomainEvent`, `DomainEvent`. Zero package references — verified. |
| `Weavo.BuildingBlocks.Application` | **Built** | `LoggingBehavior<,>` (timing + `Activity` tracing), `ValidationBehavior<,>` (FluentValidation) |
| `Weavo.BuildingBlocks.Messaging` | **Built** | `IEventBus`, `IntegrationEvent` — contracts only, no MassTransit dependency yet |
| `Weavo.BuildingBlocks.Infrastructure` | **Built** | Transactional Outbox (B.5.3) extracted from ItemMaster and now shared; Redis idempotency store for at-least-once consumption. Redis cache and audit/soft-delete interceptors still to come. |
| `Weavo.BuildingBlocks.Observability` | **Missing** | Planned: OpenTelemetry, Serilog, Prometheus, health checks |
| `Weavo.BuildingBlocks.Security` | **Missing** | Planned: claims extensions, multi-tenant context, S2S auth |

### 2.2 ItemMaster — the one complete vertical

**Domain** (`ItemMaster.Domain`)
- `ItemMasterRecord : AggregateRoot<long>` — 24 properties, all `private set`
- Behaviour methods: `UpdateStatus`, `RaiseCreatedEvent`
- Invariants mirroring the DDL: `CK_CodeFormat` (3–30 chars, alphanumeric + hyphen),
  `CK_TransactableFlags`, `CK_StockFlags`, `CK_Status`
- Events: `ItemCreatedDomainEvent`, `ItemPriceUpdatedDomainEvent`,
  `ItemStatusChangedDomainEvent`, plus an `ItemAuditEntry` model

**Application** (`ItemMaster.Application`)
- Commands/queries: `CreateItemCommand`, `UpdateItemStatusCommand`, `GetItemByIdQuery`
- `ItemCommandHandler` — depends on `IItemMasterRepository`, never on `DbContext`
- Abstractions: `IItemMasterRepository`, `ICurrentUserProvider`
- Validators: `CreateItemCommandValidator`, `UpdateItemStatusCommandValidator`

**Infrastructure** (`ItemMaster.Infrastructure`)
- `ItemMasterDbContext` — two-phase save for IDENTITY, execution-strategy transactions
- `ConvertDomainEventsToOutboxMessagesInterceptor` — both sync and async paths
- `OutboxMessage` + configuration (filtered index on pending rows)
- `ProcessOutboxMessagesJob` — `BackgroundService`, 20-row batches, 5s poll
- `ItemMasterRepository`, `ItemMasterDbContextSeeder`, `DependencyInjection`
- 2 EF migrations, applied to `WeavoMasterDB` on `192.168.3.34`

**API** (`ItemMaster.Api`)
- `ItemMasterController` — 3 endpoints: `GET /{id}`, `POST /`, `PATCH /{id}/status`
- JWT Bearer auth with a fallback policy (everything authenticated by default)
- Policies: `CanReadItems`, `CanManageItems` (claim-based)
- `GlobalExceptionHandler` → RFC 7807 ProblemDetails, with per-field validation errors
- `HttpContextCurrentUserProvider`, Swagger with bearer input
- `POST /api/v1/auth/dev-token` — **Development-only**, verified absent (404) in Production

### 2.3 Cross-cutting patterns proven in ItemMaster

- Clean Architecture layering — verified: Kernel has 0 packages, Domain → Kernel only,
  Application never references Infrastructure
- CQRS via MediatR 12 with a two-behaviour pipeline
- Transactional Outbox with at-least-once delivery and `EventId`-based dedup
- Optimistic concurrency via `rowversion`
- Soft delete (`IsDeleted`), never physical delete

---

## 3. Gap analysis vs `weavo-go.txt`

### 3.1 Gateways — 0 of 2

| Target | Status |
|---|---|
| `Weavo.YarpGateway` | Empty directory |
| `Weavo.Bff.Web` | Empty directory |

**Consequence:** no edge routing. Each service is reached directly, and there is
no central place for rate limiting or token validation.

### 3.2 PlatformServices — 0 of 7

`Identity`, `ApprovalEngine`, `DocumentManagement`, `Notification`,
`AuditLogging`, `IntegrationGateway`, `AnalyticsReporting` — all scaffolded, all empty
(4 empty layer folders each = 28 directories).

**Most urgent:** `Identity`. ItemMaster currently validates JWTs that nothing
issues, which is why the temporary `dev-token` endpoint exists.

### 3.3 MasterDataServices — 1 of 15

| Service | Status |
|---|---|
| **ItemMaster** | **Complete** — 4 layers, migrations applied |
| CustomerMaster, VendorMaster, UomMaster, CurrencyMaster, CountryMaster, TaxMaster | Empty (6 × 4 = 24 dirs) |
| OrganizationSubsystem: TenantMaster, BusinessTypeMaster, CompanyMaster, BranchMaster, DepartmentMaster, CostCenterMaster, WarehouseLocationMaster | Empty (7 × 4 = 28 dirs) |

**Note:** ItemMaster references `BaseUOMId`, `ItemCategoryId`, `ItemGroupId`,
`ItemSubGroupId`, `ItemFamilyId` as plain integers. The masters those point at do
not exist, so nothing validates them. `UomMaster` and the classification
hierarchy are the natural next builds.

### 3.4 BusinessServices — 0 of 10

`WeavoFI`, `WeavoSD`, `WeavoMM`, `WeavoWM`, `WeavoMES`, `WeavoQM`, `WeavoPM`,
`WeavoHCM`, `WeavoESS`, `WeavoPS` — all scaffolded, all empty (40 directories).

### 3.5 Supporting directories

| Target | Actual |
|---|---|
| `.github/workflows/` | Directory exists, **no workflow files** — no CI |
| `tests/ArchitectureTests/` | Empty — layer rules are documented but **not enforced** |
| `tests/Shared/*` | Empty (3 dirs) |
| `tests/Services/*` | Empty (3 dirs) |
| `deploy/k8s`, `docker-compose`, `terraform` | All empty — no local stack, no deployment |
| `docs/adr`, `api-specs`, `proto` | Empty |
| `database/` | **17 SQL files** — richer than the target tree describes |

### 3.6 Structural deviations from the target

1. **`backend/` prefix.** The target puts `src/` at the repo root; the actual
   repo nests everything under `backend/` to make room for a future `frontend/`.
   Deliberate, and worth reflecting back into `weavo-go.txt`.
2. **`.API` vs `.Api`.** The target writes `ItemMaster.API`; the build uses
   `ItemMaster.Api`. The rest of the tree still carries `.API` folders — they
   should be renamed as each service is built, for consistency.
3. **Orphaned `tests/WeavoGo.Master.Tests`** — a legacy project on disk, not in
   the solution, not building. Delete or adopt.
4. **`database/` is not in the target tree** but holds the authoritative DDL that
   ItemMaster's schema was derived from.

---

## 4. Risks and open items

### 4.1 Critical — database credentials committed to git (partially remediated)

**Working tree: resolved. Git history: still exposed.**

`backend/appsettings.json` and `backend/appsettings.Development.json` were tracked
and committed containing a live SQL password for `rabbi-it@192.168.3.34`. The
value appears in **3 commits, including `HEAD`**.

Done:
- Connection string moved to user-secrets
  (`UserSecretsId b37705f7-ac0d-417c-beb2-b85d3ae0ad61`); runtime resolution
  verified against the live database with an empty `appsettings.json`
- Both root config files deleted and untracked
- Stale `ItemMaster.API/` path untracked (superseded by `ItemMaster.Api/`)
- `.gitignore` now excludes all `appsettings*.json` except the template
- `appsettings.Example.json` added to document the required keys

**Still outstanding — the password remains readable in git history:**

1. **Rotate the SQL password.** Nothing above removes it from the three existing
   commits; anyone with repo access can still recover it. This is the only step
   that actually closes the exposure.
2. Optionally scrub history with `git filter-repo` — worth it only if the remote
   is shared; rotation is the necessary part.

### 4.2 High — dead root config — **resolved**

`backend/appsettings.json` was loaded by nothing (no `.csproj` at that level) and
its connection string carried `Trusted_Connection=true` alongside
`User Id`/`Password`, forcing Windows auth. Verified failing with
*"Cannot generate SSPI context."* Both root files have been deleted.

### 4.3 High — no architecture tests

The layer rules in `CLAUDE.md` are documentary. `tests/ArchitectureTests/` is
empty, so nothing prevents a future service from referencing Infrastructure from
Application. This is cheap to add and gets more valuable with every new service.

### 4.4 Medium — seeder unverified against SQL Server

`ItemMasterDbContextSeeder` passes 11 in-memory tests but has **never completed a
run against the real database** — `dbo.ItemMaster` is still empty. Unverified on
real hardware: the IDENTITY round-trip, the execution-strategy transaction path,
and `rowversion` population.

### 4.5 Medium — the `InitialSeed` migration is empty

`20260906062113_InitialSeed` has empty `Up()` and `Down()`. Seed data lives in the
runtime seeder, which EF cannot see. Either remove the migration or move seeding
to `HasData(...)` — not both.

### 4.6 Low — cold-connect stall to 192.168.3.34

First connection after idle hangs 10–15s; subsequent ones take ~2ms. The host is
off-subnet via the default gateway and some device builds session state on first
contact. Mitigated with `Connect Timeout=60`. Not a defect in this codebase.

### 4.7 Low — no CI

`.github/workflows/` is empty. Every build and test run so far has been local.

---

## 5. Suggested build order

1. **Rotate the leaked credential** — before anything else.
2. **`tests/ArchitectureTests`** — cheap now, prevents drift across 28 services.
3. **`PlatformServices/Identity`** — unblocks real JWTs; delete `dev-token`.
4. **Extract the Outbox** into `BuildingBlocks.Infrastructure` — it is currently
   ItemMaster-private and every service will need it. Do this *before* the second
   service is written, or the pattern gets copy-pasted 28 times.
5. **`UomMaster` + the classification hierarchy** — ItemMaster's foreign keys
   point at nothing today.
6. **`.github/workflows/build-and-test.yml`** — make the green build enforced.
7. **`deploy/docker-compose`** — a local SQL Server would have let the seeder be
   verified without the network stall.

---

## 6. Verification notes

Facts in this document were checked, not assumed:

- Project list from `dotnet sln list` and `find -name '*.csproj'`
- Build status from a full `dotnet build` (0/0)
- Layer boundaries by inspecting `ProjectReference` entries
- Applied migrations by querying `__EFMigrationsHistory` on the live database
- Credential exposure via `git ls-files` and `git log`
- The broken root connection string by attempting a real `SqlConnection.Open()`
- Empty-directory count by walking `backend/src` for leaves with no files but `.gitkeep`
