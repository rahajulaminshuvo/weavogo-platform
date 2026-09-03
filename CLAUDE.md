# WeavoGo_Platform — Agent Instructions

## Role

You are an expert .NET Solutions Architect and autonomous development agent working on
**WeavoGo_Platform**, an enterprise ERP built on Clean Architecture, CQRS, and microservices.
Write efficient, scannable code with minimal padding. Manage project structure via CLI.

**The solution already exists. Do not re-scaffold it.** Build features into the structure below.

---

## Repository layout

```
WeavoGo_Platform/
├── backend/                    ← .NET solution root; ALL C# lives here
│   ├── global.json             ← pins SDK 8.0.403 (machine default is 10.x)
│   ├── Directory.Build.props   ← shared build settings, analyzers
│   ├── Directory.Packages.props← Central Package Management (CPM)
│   ├── WeavoGo_Platform.sln
│   └── src/
│       ├── BuildingBlocks/     ← shared kernel, referenced by every service
│       ├── Gateways/           ← YARP edge proxy, BFF (scaffolded, empty)
│       ├── PlatformServices/   ← Identity, ApprovalEngine, Notification, … (scaffolded, empty)
│       ├── MasterDataServices/ ← ItemMaster (built), CustomerMaster, VendorMaster, … (empty)
│       └── BusinessServices/   ← WeavoFI, WeavoSD, WeavoMM, WeavoMES, … (scaffolded, empty)
├── database/                   ← authoritative SQL DDL (05_schema_item.sql etc.)
├── scripts/scaffold-structure.sh ← idempotent folder scaffolder
├── tests/, deploy/, docs/
└── backend-old/                ← legacy monolith, retired; do not modify
```

`ItemMaster` is the **reference implementation**. Match its patterns when building a new service.

---

## Layer rules (enforced by project references)

| Layer | Contains | May reference |
|---|---|---|
| **Domain** | Entities, value objects, enums, domain events, domain exceptions | `Weavo.BuildingBlocks.Kernel` only |
| **Application** | CQRS commands/queries/handlers, validators, DTOs, repository *interfaces* | Domain + `BuildingBlocks.Application` |
| **Infrastructure** | DbContext, EF configurations, repository *implementations*, interceptors, background jobs | Domain + Application + `BuildingBlocks.Messaging` |
| **API** | Controllers, middleware, `Program.cs` composition root | all of the above |

**The dependency arrow points inward, always.** An Application-layer handler must never take
`ItemMasterDbContext` — declare an interface in `Application/Abstractions/` and implement it in
Infrastructure. Same for anything ASP.NET-shaped: `ICurrentUserProvider` is declared in
Application and implemented over `IHttpContextAccessor` in the API layer.

`Weavo.BuildingBlocks.Kernel` has **zero** package references. Anything added there is forced on
every service in the platform.

---

## Build constraints that will bite you

These are not style preferences — code that ignores them **fails the build**.

- **`TreatWarningsAsErrors=true`** with **`AnalysisLevel=latest-recommended`**.
- **CA1848**: every `logger.LogInformation(...)` / `LogError(...)` call is an error. Use
  source-generated logging — mark the class `partial` and declare `[LoggerMessage]` methods.
  Existing EventId ranges: 1000s pipeline behaviors, 2000s command handlers, 3000s repositories,
  5000s background jobs, 6000s API middleware.
- **CA2016**: `await next()` in a MediatR behavior needs
  `#pragma warning disable CA2016` — MediatR 12's `RequestHandlerDelegate` takes no
  `CancellationToken`. The pipeline already closed over it.
- **CA2208**: `ArgumentOutOfRangeException`'s `paramName` must name a real parameter, not a field.
- **XML comments** on public members (`GenerateDocumentationFile=true`). CS1591 is suppressed in
  `NoWarn`, so missing docs won't fail — but a **malformed** `<see cref="">` will (CS1574).
  Generic types need arity: `<see cref="AggregateRoot{TId}"/>`, never `<see cref="AggregateRoot"/>`.
- **`--` is illegal inside XML comments** in `.props` / `.csproj`. Use a single hyphen. (Fine in `.cs`.)
- **Never set `InvariantGlobalization=true`** — it throws `CultureNotFoundException` at runtime on
  every SQL Server connection.

---

## Central Package Management

`.csproj` files list `<PackageReference Include="X" />` with **no `Version`**. All versions live in
`backend/Directory.Packages.props`.

Version pins that are deliberate — do not "upgrade" them:

| Package | Pinned | Why not latest |
|---|---|---|
| EF Core / providers | `8.0.30` | 9.x and 10.x cannot target `net8.0` |
| MediatR | `12.5.0` | v13+ is commercially licensed |
| MassTransit | `8.5.10` | v9+ is commercially licensed |
| Swashbuckle | `7.3.2` | — |
| FluentValidation | `11.12.0` | — |

`dotnet add package` writes a `Version` attribute and breaks CPM. Edit
`Directory.Packages.props` by hand instead.

---

## Domain event / Outbox conventions

Three non-obvious rules. Each was a silent-data-loss bug before it was fixed; regression tests
exist for all three.

**1. Never raise a creation event from a constructor when the key is IDENTITY.**
`ItemId` is `BIGINT IDENTITY`, so `Id` is `0` until the INSERT completes. An event raised in the
constructor records `ItemId = 0` forever, with no error. The aggregate sets a `_creationPending`
flag; `ItemMasterDbContext` calls `RaiseCreatedEvent()` after the insert, then saves again — both
saves inside one explicit transaction.

**2. Override *both* `SaveChanges` and `SaveChangesAsync`** (and both interceptor methods).
EF Core dispatches them independently. Handling only the async path lets any sync caller commit
successfully while silently discarding every domain event.

**3. Store type names as `"Namespace.Type, Assembly"`, never `AssemblyQualifiedName`.**
The latter embeds `Version=1.0.0.0`; after a version bump the dispatcher can no longer resolve
rows written by the previous build and parks them all as "Type resolution failed".

Also: `OutboxMessage.Id` is set from `domainEvent.EventId`, not a fresh `Guid` — consumers
deduplicate on it under at-least-once delivery.

---

## Validation

Two layers, deliberately overlapping:

- **FluentValidation** (`Application/Items/Validators/`) checks *shape* and reports every failure
  at once with per-property keys. Runs in `ValidationBehavior` before the handler.
- **Domain guards** (constructors, behavior methods) enforce invariants for *any* caller —
  importer, test, future handler — and throw on the first violation.

Both mirror the CHECK constraints in `database/*.sql`. When a rule changes, all three move together.

A `RuleFor(x => x)` object-level rule needs `.WithName("SomeKey")`, or FluentValidation reports an
empty `PropertyName` and the JSON error dictionary gets an empty-string key.

---

## Database

`database/*.sql` is currently the **authoritative** schema; the EF model mirrors it
(`ItemMasterConfiguration` maps `dbo.ItemMaster` column-for-column). No EF migrations exist yet.
Before generating the first one, decide which side owns the schema — otherwise migrations and the
hand-written DDL will fight.

---

## Working practice

1. **Plan** — name the files you will touch, by layer.
2. **Write** — full file paths, complete file contents.
3. **Verify** — `cd backend && dotnet build WeavoGo_Platform.sln`. A clean build is the minimum,
   not the goal: for anything involving persistence, events, or DI wiring, *run it*. Every serious
   bug in this codebase so far compiled perfectly.
4. **Review** — confirm no reference crosses a layer boundary inward-out.

If a build fails with `MSB3021`/`MSB3027` (file locked), a previous `dotnet run` is still holding
the output directory. Stop it, then `rm -rf src/.../bin src/.../obj`.

### Local run

```bash
cd backend/src/MasterDataServices/ItemMaster/ItemMaster.Api
dotnet user-secrets set "JwtSettings:Secret" "<32+ char key>"   # once; never commit a secret
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

Every endpoint requires a JWT (`SetFallbackPolicy` + `RequireAuthenticatedUser`), except
`/health/live` and `/health/ready`. Swagger UI at `/swagger` has a bearer input.

To get a token: `POST /api/v1/auth/dev-token` (anonymous) returns one carrying
`item_master:read` + `item_master:write` and user id 1001. Paste it into Swagger's Authorize
dialog. **This route is mapped only when `IsDevelopment()`** — verified absent (404) under
`ASPNETCORE_ENVIRONMENT=Production`. **Delete it** once `PlatformServices/Identity` issues real
tokens; it is a stand-in, not a design.

Note that the fallback policy challenges unmatched paths too, so an unauthenticated request to a
nonexistent route returns 401 rather than 404. Send a valid token when you need to tell "route
missing" apart from "not signed in".

---

## Style

C# 12 / .NET 8. File-scoped namespaces, primary constructors, collection expressions, `required`
members. Concise — no ceremony, no restating what the code says. Comment the *why* when a decision
is non-obvious (a timing hazard, a version constraint, a deliberate trade-off); skip the *what*.
