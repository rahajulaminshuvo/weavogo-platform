/* =============================================================================
   Universal Item Master — migrate.sql
   Runs the whole migration against a fresh (or partly built) database, in order.

   Requires sqlcmd with SQLCMD mode enabled — the :r directives are sqlcmd
   includes, not T-SQL. Run from this folder:

       sqlcmd -S localhost -U sa -P '<password>' -C -b -i migrate.sql

   In SSMS: Query -> SQLCMD Mode, then open and execute this file.
   Every script is individually idempotent, so re-running is safe.
   ============================================================================= */
:on error exit

PRINT '== 00 create database ==';
:r 00_create_database.sql
PRINT '== 01 platform ==';
:r 01_schema_platform.sql
PRINT '== 02 organization ==';
:r 02_schema_organization.sql
PRINT '== 03 uom ==';
:r 03_schema_uom.sql
PRINT '== 04 classification ==';
:r 04_schema_classification.sql
PRINT '== 05 item ==';
:r 05_schema_item.sql
PRINT '== 06 functional ==';
:r 06_schema_functional.sql
PRINT '== 07 quality ==';
:r 07_schema_quality.sql
PRINT '== 08 governance ==';
:r 08_schema_governance.sql
PRINT '== 09 indexes + audit FKs ==';
:r 09_indexes_and_audit_fks.sql
PRINT '== 10 triggers ==';
:r 10_triggers.sql
PRINT '== 11 reference seed ==';
:r 11_seed_reference.sql
PRINT '== 12 sample data seed ==';
:r 12_seed_sample_data.sql
PRINT '== migration complete ==';
