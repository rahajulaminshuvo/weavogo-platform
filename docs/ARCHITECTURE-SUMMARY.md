# WeavoGo_Platform — Architectural Implementation Summary

Verified snapshot of what is built, measured against the target architecture
(`weavo-go.txt`) and the SDS (`docs/instruction.txt`, Sections B.3–B.11).

**Date:** 2026-09-09 · **Branch:** `core/identity` · **Build:** 0 warnings, 0 errors
**Scope:** 8 projects, 37 C# files, 1 service vertical, 4 of 6 BuildingBlocks.

---

## 1. Position

| Metric | Value |
|---|---|
| Projects in solution | **8** of ~120 planned |
| Service verticals complete | **1** (ItemMaster) of 29 |
| BuildingBlocks libraries | **4** of 6 |
| Empty scaffolded directories | **125** |
| EF migrations applied to `WeavoMasterDB` | 2 |
| Overall completion | **~7%** by project count |

The skeleton is fully scaffolded; one vertical is complete and serves as the
reference implementation the remaining 28 are cloned from. Shared infrastructure
is where the real leverage sits: the four BuildingBlocks now carry the patterns
(Outbox, idempotency, CQRS pipeline, event bus) that every future service
inherits rather than reimplements.

---

## 2. What is built

### 2.1 BuildingBlocks — 4 of 6

| Project | Files | Contents |
|---|---|---|
| `.Kernel` | 4 | `Entity<TId>`, `AggregateRoot<TId>`, `IDomainEvent`/`DomainEvent`, `IHasDomainEvents`. **Zero package references** — verified. |
| `.Application` | 2 | `LoggingBehavior<,>` (timing + `Activity` span), `ValidationBehavior<,>` (FluentValidation) |
| `.Messaging` | 4 | `IEventBus`, `IntegrationEvent`, `MassTransitEventBus`, `MessagingConfiguration` (queue topology, retry, DLQ) |
| `.Infrastructure` | 5 | Transactional Outbox (entity, interceptor, EF config) + Redis idempotency store |
| `.Observability` | — | **Missing.** OpenTelemetry, Serilog, Prometheus (B.9) |
| `.Security` | — | **Missing.** Claims extensions, tenant context, S2S auth (B.7) |

`IHasDomainEvents` exists because `AggregateRoot<TId>` is generic in its key:
shared infrastructure cannot filter the EF change tracker for it without knowing
every key type. ItemMaster uses `long`; other contexts will use `Guid`.

### 2.2 ItemMaster — the one complete vertical

**Domain** (2 files) — `ItemMasterRecord : AggregateRoot<long>`, 24 properties all
`private set`. Invariants mirror the DDL's CHECK constraints (`CK_CodeFormat`,
`CK_TransactableFlags`, `CK_StockFlags`, `CK_Status`). Three domain events.

**Application** (4 files) — `CreateItemCommand`, `UpdateItemStatusCommand`,
`GetItemByIdQuery`; `ItemCommandHandler` depends on `IItemMasterRepository`,
never on `DbContext`; two validators.

**Infrastructure** (12 files) — `ItemMasterDbContext` with two-phase save for
IDENTITY and execution-strategy transactions; repository; seeder; integration-event
translator; Outbox dispatcher; 2 migrations.

**API** (4 files) — 3 REST endpoints, JWT Bearer with a fallback policy,
claim-based authorization, RFC 7807 error handling, Swagger with bearer input.

### 2.3 Integration architecture (B.5) — implemented

- **B.5.2 Async events** — MassTransit over RabbitMQ. Queue naming
  `<service>-<event>`; one durable queue per service per event, so a slow consumer
  cannot block another service.
- **B.5.3 Outbox** — event row and business data commit in one local transaction.
  Both sync and async save paths drained. Type names stored version-agnostically.
  `OutboxMessage.Id` carries the domain event's `EventId` as the dedup key.
- **B.5.3 Idempotency** — Redis `SET NX` in one atomic round trip, keyed per
  consumer. In-memory fallback for local development (single-process only).
- **Resilience** — 3 immediate retries at 5s, then delayed redelivery at 1/5/15
  min, then `<queue>_error`. Dispatcher abandons a row after 5 attempts so one
  poisoned message cannot block the queue.

Domain events stay inside the bounded context; only translated
`IntegrationEvent` contracts cross service boundaries.

---

## 3. Gap against the target

| Area | Built | Target |
|---|---|---|
| Gateways (YARP, BFF) | 0 | 2 |
| PlatformServices | 0 | 7 |
| MasterDataServices | 1 | 15 |
| BusinessServices | 0 | 10 |
| BuildingBlocks | 4 | 6 |
| ArchitectureTests | 0 | 1 |

### 3.1 Ordering constraints that matter

**Identity blocks everything.** ItemMaster validates JWTs that nothing issues; a
Development-only `POST /api/v1/auth/dev-token` endpoint is the stand-in. Until
`PlatformServices/Identity` exists, no service can authenticate a real caller.
The `core/identity` branch is open but empty.

**Master data has dangling references.** `ItemMasterRecord` carries
`BaseUOMId`, `ItemCategoryId`, `ItemGroupId`, `ItemSubGroupId`, `ItemFamilyId`
as plain integers. Nothing validates them, because `UomMaster` and the
classification hierarchy do not exist.

**No architecture tests.** Layer rules are documented in `CLAUDE.md` and hold
today (verified below), but nothing enforces them. Every new service is an
opportunity for drift, and the cost of adding the tests rises with each one.

---

## 4. Verified invariants

Checked against the code, not assumed:

| Rule | Status |
|---|---|
| `Weavo.BuildingBlocks.Kernel` has zero package references | Holds |
| `ItemMaster.Domain` references only `Weavo.BuildingBlocks.Kernel` | Holds |
| `ItemMaster.Application` does not reference Infrastructure | Holds |
| `dotnet build WeavoGo_Platform.sln` | 0 warnings, 0 errors |

---

## 5. Open risks

### 5.1 High — model has drifted from the database

`OutboxMessage.AttemptCount` exists in the entity and is queried by the
dispatcher, but appears in **no migration and no model snapshot**. The
`OutboxMessages` table on `WeavoMasterDB` has no such column, so the dispatcher's
`Where(m => m.AttemptCount < MaxAttempts)` will fail at runtime against the real
database. The in-memory tests pass because that provider builds the schema from
the model.

```bash
cd backend
dotnet ef migrations add SharedOutboxAttemptCount \
  --project src/MasterDataServices/ItemMaster/ItemMaster.Infrastructure/ItemMaster.Infrastructure.csproj \
  --startup-project src/MasterDataServices/ItemMaster/ItemMaster.Api/ItemMaster.Api.csproj \
  --output-dir Persistence/Migrations
dotnet ef database update --project … --startup-project …
```

### 5.2 High — SQL credential exposed in git history

The working tree is clean and the secret now lives in user-secrets, but the
password for `rabbi-it@192.168.3.34` remains readable in three commits on
`origin`. History was rewritten locally; the force-push and **password rotation**
are still outstanding. Rotation is the step that actually closes the exposure.

### 5.3 Medium — RabbitMQ never exercised

The publish path was verified through MassTransit's in-memory test harness, which
covers serialisation, routing and headers but not the broker. `UseDelayedMessageScheduler()`
requires the `rabbitmq_delayed_message_exchange` plugin; without it, delayed
redelivery fails at start-up.

### 5.4 Medium — seeder unverified against SQL Server

`ItemMasterDbContextSeeder` passes 11 in-memory tests but has never completed a
run against the real database — `dbo.ItemMaster` is still empty. The IDENTITY
round-trip, execution-strategy transaction and `rowversion` population are
unproven on real hardware.

### 5.5 Low — empty `InitialSeed` migration

`20260906062113_InitialSeed` has empty `Up()` and `Down()`. Seed data lives in the
runtime seeder, which EF cannot see. Harmless but misleading.

### 5.6 Low — no CI

`.github/workflows/` is empty. Every build and test so far has been local.

---

## 6. Recommended order

1. **Rotate the leaked SQL credential** — the only outstanding step that closes a
   live exposure.
2. **Add the `AttemptCount` migration** — the code currently cannot run against
   the real database.
3. **`tests/ArchitectureTests`** — cheap now, prevents drift across 28 services,
   and is a stated SDS requirement (B.11.2).
4. **`PlatformServices/Identity`** — unblocks real JWTs; then delete `dev-token`.
   Note A.12.2 defines `UserAccount` with only four columns and no password field;
   auth state belongs in a separate `UserCredential` table.
5. **`UomMaster` + classification hierarchy** — resolves ItemMaster's dangling FKs.
6. **`deploy/docker-compose`** — a local SQL Server and RabbitMQ would let 5.3 and
   5.4 be verified without the network stall to 192.168.3.34.
7. **`BuildingBlocks.Observability`** — B.9's traceId propagation belongs there
   once, before 28 services each invent their own.

---

## 7. How this was verified

- Project list from `dotnet sln list` and `find -name '*.csproj'`
- Build status from a full `dotnet build` (0/0)
- Layer boundaries by reading `ProjectReference` and `PackageReference` entries
- Migration drift by grepping the migrations directory for `AttemptCount`
- Credential exposure via `git log` and `git grep` against `HEAD`
- Empty-directory count by walking `backend/src` for leaves holding only `.gitkeep`
