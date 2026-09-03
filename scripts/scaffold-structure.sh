#!/usr/bin/env bash
#
# WeavoGo_Platform :: Architecture Scaffolding Script
# ---------------------------------------------------
# Creates the full Clean Architecture / CQRS / Microservices folder tree.
# Every directory receives a .gitkeep so Git tracks the empty architecture.
#
# Idempotent : safe to re-run; mkdir -p and .gitkeep creation never destroy.
# Additive   : does not touch or move any pre-existing folders.
#
# Usage: bash scripts/scaffold-structure.sh [target-root]
#        Defaults to the repository root (the parent of this script's directory).

set -euo pipefail

# ---------------------------------------------------------------------------
# Resolve target root
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="${1:-$(cd "$SCRIPT_DIR/.." && pwd)}"

# ---------------------------------------------------------------------------
# Console helpers (colour only when attached to a terminal)
# ---------------------------------------------------------------------------
if [ -t 1 ]; then
  ESC=$(printf '\033')
  C_RESET="${ESC}[0m"
  C_DIM="${ESC}[2m"
  C_BOLD="${ESC}[1m"
  C_GREEN="${ESC}[32m"
  C_CYAN="${ESC}[36m"
  C_YELLOW="${ESC}[33m"
else
  C_RESET=""; C_DIM=""; C_BOLD=""; C_GREEN=""; C_CYAN=""; C_YELLOW=""
fi

DIR_COUNT=0
KEEP_COUNT=0

section() {
  printf '\n%s==> %s%s\n' "${C_BOLD}${C_CYAN}" "$1" "${C_RESET}"
}

# keep <absolute-path>
#   Ensures a single directory holds a .gitkeep marker.
keep() {
  if [ ! -f "${1}/.gitkeep" ]; then
    : > "${1}/.gitkeep"
    KEEP_COUNT=$((KEEP_COUNT + 1))
  fi
}

# make <relative-path>
#   Creates the directory (recursively) and drops a .gitkeep into the leaf
#   AND into every intermediate directory along the way. mkdir -p creates
#   grouping folders (src/PlatformServices, tests/Shared, ...) implicitly, and
#   those must be tracked too -- otherwise Git silently drops them.
make() {
  local rel="$1"
  local abs="${ROOT}/${rel}"

  if [ ! -d "$abs" ]; then
    mkdir -p "$abs"
    DIR_COUNT=$((DIR_COUNT + 1))
    printf '  %s+%s %s\n' "${C_GREEN}" "${C_RESET}" "$rel"
  else
    printf '  %s=%s %s %s(exists)%s\n' "${C_DIM}" "${C_RESET}" "$rel" "${C_DIM}" "${C_RESET}"
  fi

  # Walk each path segment, marking the leaf and all of its ancestors.
  local walked="${ROOT}"
  local IFS='/'
  for segment in $rel; do
    [ -z "$segment" ] && continue
    walked="${walked}/${segment}"
    keep "$walked"
  done
}

# layers <parent-path> <project-prefix>
#   Creates the canonical Clean Architecture quartet:
#     <parent>/<prefix>.Domain
#     <parent>/<prefix>.Application
#     <parent>/<prefix>.Infrastructure
#     <parent>/<prefix>.API
layers() {
  local parent="$1"
  local prefix="$2"
  make "${parent}/${prefix}.Domain"
  make "${parent}/${prefix}.Application"
  make "${parent}/${prefix}.Infrastructure"
  make "${parent}/${prefix}.API"
}

printf '%sWeavoGo_Platform :: Architecture Scaffolding%s\n' "${C_BOLD}" "${C_RESET}"
printf 'Target root: %s%s%s\n' "${C_YELLOW}" "${ROOT}" "${C_RESET}"

# ---------------------------------------------------------------------------
# 0. Repository root -- CI/CD pipelines & documentation
# ---------------------------------------------------------------------------
section "CI/CD & Documentation"
make ".github/workflows"
make "docs/adr"
make "docs/api-specs"
make "docs/proto"

# ---------------------------------------------------------------------------
# 1. BuildingBlocks -- shared kernel & cross-cutting infrastructure
# ---------------------------------------------------------------------------
section "src/BuildingBlocks -- Shared Kernel"
BUILDING_BLOCKS=(
  Kernel          # Entity, AggregateRoot, ValueObject, IDomainEvent
  Application     # CQRS pipeline behaviors: validation, logging, perf, UoW
  Infrastructure  # EF Core interceptors, Redis cache, Outbox pattern
  Messaging       # MassTransit/RabbitMQ wrappers, integration events
  Observability   # OpenTelemetry, Serilog, Prometheus, health checks
  Security        # Claims extensions, tenant context, S2S auth
)
for bb in "${BUILDING_BLOCKS[@]}"; do
  make "src/BuildingBlocks/Weavo.BuildingBlocks.${bb}"
done

# ---------------------------------------------------------------------------
# 2. Gateways -- edge routing & backend-for-frontend
# ---------------------------------------------------------------------------
section "src/Gateways -- Edge & BFF"
make "src/Gateways/Weavo.YarpGateway"
make "src/Gateways/Weavo.Bff.Web"

# ---------------------------------------------------------------------------
# 3. PlatformServices -- shared enterprise infrastructure services
# ---------------------------------------------------------------------------
section "src/PlatformServices -- Enterprise Infrastructure"
PLATFORM_SERVICES=(
  Identity            # SSO, OIDC, multi-tenant context
  ApprovalEngine      # dynamic multi-tier workflow & approval engine
  DocumentManagement  # DMS: S3/MinIO storage, versioning, previews
  Notification        # email / SMS / SignalR / mobile push dispatcher
  AuditLogging        # centralised audit trails & compliance traces
  IntegrationGateway  # EDI, banking ISO20022/MT940, external webhooks
  AnalyticsReporting  # ClickHouse / read-replica reporting engines
)
for svc in "${PLATFORM_SERVICES[@]}"; do
  layers "src/PlatformServices/${svc}" "${svc}"
done

# ---------------------------------------------------------------------------
# 4a. MasterDataServices / OrganizationSubsystem -- multi-tenant org structure
# ---------------------------------------------------------------------------
section "src/MasterDataServices/OrganizationSubsystem -- Org Structure"
ORG_MASTERS=(
  TenantMaster
  BusinessTypeMaster
  CompanyMaster
  BranchMaster
  DepartmentMaster
  CostCenterMaster
  WarehouseLocationMaster
)
for org in "${ORG_MASTERS[@]}"; do
  layers "src/MasterDataServices/OrganizationSubsystem/${org}" "${org}"
done

# ---------------------------------------------------------------------------
# 4b. MasterDataServices -- fine-grained master data domains
# ---------------------------------------------------------------------------
section "src/MasterDataServices -- Master Data Domains"
MASTER_SERVICES=(
  ItemMaster      # materials, finished goods, raw materials, SKUs, variants
  CustomerMaster  # customers, bill-to/ship-to addresses, credit terms
  VendorMaster    # suppliers, subcontractors, payment terms, bank details
  UomMaster       # units of measure & conversion rules
  CurrencyMaster  # multi-currency registry, exchange rates
  CountryMaster   # countries, states/provinces, cities, postal codes
  TaxMaster       # tax structures, VAT, duty rates, HSN/SAC codes
)
for svc in "${MASTER_SERVICES[@]}"; do
  layers "src/MasterDataServices/${svc}" "${svc}"
done

# ---------------------------------------------------------------------------
# 5. BusinessServices -- core Weavo ERP modules
# ---------------------------------------------------------------------------
section "src/BusinessServices -- Core ERP Modules"
BUSINESS_SERVICES=(
  WeavoFI   # Financial Accounting & Controlling (GL, AP, AR, cost centers)
  WeavoSD   # Sales & Distribution (order-to-cash, pricing, invoicing)
  WeavoMM   # Materials Management & Procurement
  WeavoWM   # Warehouse Management & Inventory
  WeavoMES  # Manufacturing Execution & Shop Floor Control
  WeavoQM   # Quality Management (inspection lots, sampling, CAPA)
  WeavoPM   # Plant Maintenance (equipment, work orders, preventive)
  WeavoHCM  # Human Capital Management (HRIS, attendance, payroll)
  WeavoESS  # Employee Self Service (leave, approval, payslip)
  WeavoPS   # Project Systems & Portfolio Management (WBS, costing)
)
for svc in "${BUSINESS_SERVICES[@]}"; do
  layers "src/BusinessServices/${svc}" "${svc}"
done

# ---------------------------------------------------------------------------
# 6. tests -- centralised automated test suite
# ---------------------------------------------------------------------------
section "tests -- Automated Test Suite"
make "tests/ArchitectureTests"
make "tests/Shared/Weavo.Tests.Shared.Kernel"
make "tests/Shared/Weavo.Tests.Shared.Messaging"
make "tests/Shared/Weavo.Tests.Shared.TestContainers"
make "tests/Services/Weavo.MES.Tests"
make "tests/Services/Weavo.HCM.Tests"
make "tests/Services/Weavo.FI.Tests"

# ---------------------------------------------------------------------------
# 7. deploy -- infrastructure as code & deployment scripts
# ---------------------------------------------------------------------------
section "deploy -- Infrastructure & Deployment"
make "deploy/k8s"
make "deploy/docker-compose"
make "deploy/terraform"

# ---------------------------------------------------------------------------
# Verification & summary
# ---------------------------------------------------------------------------
SCAFFOLD_ROOTS=()
for candidate in "src" "tests" "deploy" "docs" ".github"; do
  [ -d "${ROOT}/${candidate}" ] && SCAFFOLD_ROOTS+=("${ROOT}/${candidate}")
done

TOTAL_DIRS=$(find "${SCAFFOLD_ROOTS[@]}" -type d | wc -l | tr -d '[:space:]')
TOTAL_KEEPS=$(find "${SCAFFOLD_ROOTS[@]}" -type f -name '.gitkeep' | wc -l | tr -d '[:space:]')

printf '\n%sScaffolding complete.%s\n' "${C_BOLD}${C_GREEN}" "${C_RESET}"
printf '  Directories created this run  : %s\n' "${DIR_COUNT}"
printf '  .gitkeep files added this run : %s\n' "${KEEP_COUNT}"
printf '  Total scaffolded directories  : %s\n' "${TOTAL_DIRS}"
printf '  Total .gitkeep files present  : %s\n' "${TOTAL_KEEPS}"

if [ "${TOTAL_DIRS}" != "${TOTAL_KEEPS}" ]; then
  printf '\n%sWARNING%s: directory/.gitkeep count mismatch. Directories missing a .gitkeep:\n' \
    "${C_YELLOW}" "${C_RESET}"
  find "${SCAFFOLD_ROOTS[@]}" -type d -exec sh -c '[ -f "$1/.gitkeep" ] || echo "  $1"' _ {} \;
  exit 1
fi

printf '\n%sEvery directory contains a .gitkeep -- the empty architecture is Git-trackable.%s\n' \
  "${C_DIM}" "${C_RESET}"
printf '%sNext: git add -A && git commit -m "chore: scaffold platform architecture"%s\n' \
  "${C_DIM}" "${C_RESET}"
