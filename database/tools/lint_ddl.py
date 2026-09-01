#!/usr/bin/env python3
"""
Structural linter for the Universal Item Master DDL.

No SQL Server required. Parses the numbered migration scripts in order and checks:

  1. Parenthesis balance per CREATE TABLE block.
  2. Every table is created exactly once.
  3. Every FOREIGN KEY target table exists and was created earlier in the order.
  4. Every FOREIGN KEY target column exists on the target table.
  5. Every FOREIGN KEY source column exists on the source table.
  6. Constraint names are globally unique.
  7. Every table carries the seven standard audit columns (SDS 4.1).
  8. Seed INSERTs name only real columns, and column/value counts line up.

Usage:  python3 tools/lint_ddl.py [database_dir]
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

SCRIPTS = [
    "01_schema_platform.sql",
    "02_schema_organization.sql",
    "03_schema_uom.sql",
    "04_schema_classification.sql",
    "05_schema_item.sql",
    "06_schema_functional.sql",
    "07_schema_quality.sql",
    "08_schema_governance.sql",
]
SEED_SCRIPTS = ["11_seed_reference.sql", "12_seed_sample_data.sql"]

AUDIT_COLUMNS = {
    "createdby", "createddate", "modifiedby", "modifieddate",
    "isactive", "isdeleted", "rowversion",
}

CREATE_RE = re.compile(r"CREATE\s+TABLE\s+dbo\.(\w+)\s*\(", re.IGNORECASE)
FK_RE = re.compile(
    r"CONSTRAINT\s+(\w+)\s+FOREIGN\s+KEY\s*\(\s*(\w+)\s*\)\s*"
    r"(?:\r?\n\s*)?REFERENCES\s+dbo\.(\w+)\s*\(\s*(\w+)\s*\)",
    re.IGNORECASE,
)
CONSTRAINT_NAME_RE = re.compile(r"CONSTRAINT\s+(\w+)\s", re.IGNORECASE)
INSERT_RE = re.compile(r"INSERT\s+(?:INTO\s+)?dbo\.(\w+)\s*\(([^)]*)\)", re.IGNORECASE)

errors: list[str] = []
warnings: list[str] = []


def strip_comments(text: str) -> str:
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.DOTALL)
    text = re.sub(r"--[^\n]*", " ", text)
    return text


def table_body(text: str, start: int) -> tuple[str, int]:
    """Return the parenthesised list starting at the '(' index `start`.

    String literals are skipped so that parentheses and commas inside them
    (JSON snippets, prose in descriptions) do not confuse the scan.
    """
    depth, i, n = 0, start, len(text)
    while i < n:
        ch = text[i]
        if ch == "'":
            i += 1
            while i < n:
                if text[i] == "'":
                    if i + 1 < n and text[i + 1] == "'":
                        i += 2
                        continue
                    break
                i += 1
        elif ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
            if depth == 0:
                return text[start + 1:i], i
        i += 1
    raise ValueError("unbalanced parentheses")


def split_top_level(body: str) -> list[str]:
    parts, depth, cur = [], 0, []
    i, n = 0, len(body)
    while i < n:
        ch = body[i]
        if ch == "'":
            cur.append(ch)
            i += 1
            while i < n:
                cur.append(body[i])
                if body[i] == "'":
                    if i + 1 < n and body[i + 1] == "'":
                        cur.append(body[i + 1])
                        i += 2
                        continue
                    break
                i += 1
            i += 1
            continue
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth -= 1
        if ch == "," and depth == 0:
            parts.append("".join(cur))
            cur = []
        else:
            cur.append(ch)
        i += 1
    if cur:
        parts.append("".join(cur))
    return [p.strip() for p in parts if p.strip()]


def main() -> int:
    base = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).resolve().parent.parent)

    # SchemaMigration is created in 00_create_database.sql (bookkeeping, no audit columns).
    tables: dict[str, set[str]] = {"SchemaMigration": {"migrationid", "scriptname", "applieddate"}}
    table_order: dict[str, int] = {}
    constraint_names: dict[str, str] = {}     # name -> "script:table"

    for order, script in enumerate(SCRIPTS):
        path = base / script
        if not path.exists():
            errors.append(f"{script}: missing")
            continue
        raw = path.read_text()
        text = strip_comments(raw)

        for m in CREATE_RE.finditer(text):
            name = m.group(1)
            try:
                body, _ = table_body(text, m.end() - 1)
            except ValueError:
                errors.append(f"{script}: unbalanced parentheses in CREATE TABLE {name}")
                continue

            if name in tables:
                errors.append(f"{script}: table {name} created more than once")
            cols: set[str] = set()
            for part in split_top_level(body):
                head = part.split(None, 1)
                if not head:
                    continue
                kw = head[0].upper()
                if kw in {"CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "INDEX"}:
                    continue
                cols.add(head[0].strip("[]").lower())
            tables[name] = cols
            table_order[name] = order

            missing_audit = AUDIT_COLUMNS - cols
            if missing_audit:
                errors.append(
                    f"{script}: {name} is missing audit column(s): "
                    + ", ".join(sorted(missing_audit))
                )

        for m in CONSTRAINT_NAME_RE.finditer(text):
            cname = m.group(1)
            if cname in constraint_names:
                errors.append(
                    f"{script}: duplicate constraint name {cname} "
                    f"(already used in {constraint_names[cname]})"
                )
            else:
                constraint_names[cname] = script

    # Foreign keys — resolved after every table is known, then ordered.
    for order, script in enumerate(SCRIPTS):
        path = base / script
        if not path.exists():
            continue
        text = strip_comments(path.read_text())
        # Attribute each FK to the table whose CREATE block contains it.
        creates = [(m.group(1), m.start()) for m in CREATE_RE.finditer(text)]
        for m in FK_RE.finditer(text):
            cname, src_col, tgt_table, tgt_col = m.groups()
            src_table = None
            for tname, pos in creates:
                if pos < m.start():
                    src_table = tname
                else:
                    break
            if tgt_table not in tables:
                errors.append(f"{script}: {cname} references unknown table dbo.{tgt_table}")
                continue
            if tgt_col.lower() not in tables[tgt_table]:
                errors.append(
                    f"{script}: {cname} references unknown column {tgt_table}.{tgt_col}"
                )
            if src_table and src_col.lower() not in tables.get(src_table, set()):
                errors.append(
                    f"{script}: {cname} declares FK on unknown column {src_table}.{src_col}"
                )
            if table_order.get(tgt_table, 99) > order:
                errors.append(
                    f"{script}: {cname} references dbo.{tgt_table}, which is created "
                    f"later ({SCRIPTS[table_order[tgt_table]]}) — migration order is wrong"
                )

    # Seed scripts — column names and value-count sanity.
    for script in SEED_SCRIPTS:
        path = base / script
        if not path.exists():
            errors.append(f"{script}: missing")
            continue
        text = strip_comments(path.read_text())
        for m in INSERT_RE.finditer(text):
            tname, collist = m.group(1), m.group(2)
            if tname not in tables:
                errors.append(f"{script}: INSERT into unknown table dbo.{tname}")
                continue
            cols = [c.strip().strip("[]") for c in collist.split(",") if c.strip()]
            unknown = [c for c in cols if c.lower() not in tables[tname]]
            if unknown:
                errors.append(
                    f"{script}: INSERT dbo.{tname} names unknown column(s): {', '.join(unknown)}"
                )
            # Value tuples following this INSERT, up to the next statement terminator.
            tail = text[m.end():]
            stop = re.search(r"\bGO\b|\bSET\s+IDENTITY_INSERT\b|\bIF\s+NOT\s+EXISTS\b", tail, re.IGNORECASE)
            segment = tail[: stop.start()] if stop else tail
            vstart = re.search(r"\bVALUES\b", segment, re.IGNORECASE)
            if not vstart:
                continue
            pos, tuple_no = vstart.end(), 0
            while pos < len(segment):
                nxt = segment.find("(", pos)
                if nxt < 0:
                    break
                try:
                    tup, end = table_body(segment, nxt)
                except ValueError:
                    break
                tuple_no += 1
                vals = split_top_level(tup)
                if len(vals) != len(cols):
                    errors.append(
                        f"{script}: INSERT dbo.{tname} tuple #{tuple_no} — {len(cols)} "
                        f"columns but {len(vals)} values: ({tup[:70].strip()}...)"
                    )
                after = segment[end + 1:]
                if not re.match(r"\s*,", after):
                    break
                pos = end + 1

    print(f"Tables parsed: {len(tables)}")
    print(f"Named constraints: {len(constraint_names)}")
    for w in warnings:
        print(f"WARN  {w}")
    for e in errors:
        print(f"ERROR {e}")
    print("RESULT:", "FAIL" if errors else "PASS")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
