#!/usr/bin/env bash
# =============================================================================
# Universal Item Master — run-migrations.sh
# Applies every migration script in order, then runs the verification suite.
#
#   ./run-migrations.sh                       # localhost, sa, $SA_PASSWORD
#   SQL_SERVER=host,1433 SA_PASSWORD='...' ./run-migrations.sh
#   ./run-migrations.sh --no-sample-data      # schema + reference data only
#   ./run-migrations.sh --verify-only
# =============================================================================
set -euo pipefail

SQL_SERVER="${SQL_SERVER:-localhost,1433}"
SQL_USER="${SQL_USER:-sa}"
SA_PASSWORD="${SA_PASSWORD:-}"
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ -z "$SA_PASSWORD" ]]; then
  echo "SA_PASSWORD is not set. Export it, or pass it inline:" >&2
  echo "  SA_PASSWORD='Your_password123' $0" >&2
  exit 1
fi

SQLCMD="$(command -v sqlcmd || true)"
if [[ -z "$SQLCMD" ]]; then
  echo "sqlcmd not found on PATH." >&2
  echo "Install the mssql-tools package, or run this inside the SQL Server container:" >&2
  echo "  docker exec -i sqlserver /opt/mssql-tools18/bin/sqlcmd ..." >&2
  exit 1
fi

run() {
  local script="$1"
  echo "--> $script"
  "$SQLCMD" -S "$SQL_SERVER" -U "$SQL_USER" -P "$SA_PASSWORD" -C -b -I -i "$HERE/$script"
}

SCHEMA_SCRIPTS=(
  00_create_database.sql
  01_schema_platform.sql
  02_schema_organization.sql
  03_schema_uom.sql
  04_schema_classification.sql
  05_schema_item.sql
  06_schema_functional.sql
  07_schema_quality.sql
  08_schema_governance.sql
  09_indexes_and_audit_fks.sql
  10_triggers.sql
  11_seed_reference.sql
)

case "${1:-}" in
  --verify-only)
    run 99_verify.sql
    exit 0
    ;;
  --no-sample-data)
    for s in "${SCHEMA_SCRIPTS[@]}"; do run "$s"; done
    ;;
  *)
    for s in "${SCHEMA_SCRIPTS[@]}"; do run "$s"; done
    run 12_seed_sample_data.sql
    run 99_verify.sql
    ;;
esac

echo "Migration complete."
