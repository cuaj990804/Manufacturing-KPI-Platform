# Demo database

Create a SQL Server database for the public GDIKPI application without restoring a production backup.

## Contents

| File | Purpose |
| --- | --- |
| `00-create-database.sql` | Create `ManufacturingKpiDemo`; refuse to replace an existing database. |
| `01-schema.sql` | Create 28 tables, keys, relationships, defaults, and indexes in an empty database. |
| `02-views-and-procedures.sql` | Create five views and five stored procedures. |
| `03-demo-data.sql` | Generate synthetic users, permissions, lines, operators, production, scans, and quality records. |
| `04-verify.sql` | Check object counts, demo counts, local dependencies, views, and all five procedures. |

The schema was extracted from the latest backup set dated 2026-09-15. SQL Server diagram support objects are omitted. The EF migration-history table is included empty; no migration history or other source rows are exported. References to the original database in procedures are replaced with local `dbo` references. No source database users, logins, server paths, or database backups are included.

## Requirements

- SQL Server 2019 or later, including Express.
- `sqlcmd` and a Windows account allowed to create a local development database.
- The application requirements listed in the [main README](../README.md).

## Install

Run these commands in PowerShell from the repository root. Change the example instance name to your local SQL Server instance. Keep `-b`: it stops execution on SQL errors.

```powershell
$instance = '.\SQLEXPRESS'
sqlcmd -S $instance -E -b -d master -i database/00-create-database.sql
if ($LASTEXITCODE -ne 0) { throw 'Database creation failed; no existing database was replaced.' }

foreach ($file in @('01-schema.sql', '02-views-and-procedures.sql', '03-demo-data.sql', '04-verify.sql')) {
    sqlcmd -S $instance -E -b -f 65001 -d ManufacturingKpiDemo -i (Join-Path database $file)
    if ($LASTEXITCODE -ne 0) { throw "Database setup failed: $file" }
}

$env:ConnectionStrings__DefaultConnection = "Server=$instance;Database=ManufacturingKpiDemo;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=True;"
dotnet run --project GDIKPI/GDIKPI.csproj --launch-profile http
```

If `ManufacturingKpiDemo` already exists, do not delete it to retry. Create a differently named empty development database, skip step `00`, and use that name in the remaining commands and application connection string. Schema creation and seeding are intentionally one-time operations; the seed script refuses a database containing application rows.

The complete schema already includes the operator-line and dashboard-settings changes. Do not additionally run the incremental scripts in `GDIKPI/Scripts` for this fresh database.

## Explore

Open `http://localhost:5016` and enter a demo employee number:

- `900001`: demo administrator with permissions for all demo modules and lines.
- `900002`: demo supervisor with view permissions for the first area and line.
- `L1` or `L2`: the application's production-line sign-in flow.

The current application signs in using an employee or line identifier alone. These public demo accounts are for local evaluation; this is not authentication suitable for an Internet-facing deployment.

Synthetic content includes two areas, two lines, three production operators, 140 hourly production records, 2,520 operator scans, 1,680 line scans, and seven quality events. Full sample shifts cover the installation date and preceding six days, so some sample times on the installation date can be in the future. Data is generated independently and is not an anonymized copy of production records.

Useful pages:

- `/DashboardProduction/Index?areaId=1`
- `/ProductionOperators/Index?areaId=1`
- `/ProductionOperatorsDashboard/Index`
- `/ProductionLinesScanner/Index?areaId=1`

External part-number and attendance services are not included. Workflows that call those services still require local implementations or configured development endpoints. Historical OEE and efficiency tables start empty; their scheduled calculations are separate from the seed data. The data set does not exercise every application workflow.

## Validation

The scripts were executed against a newly created SQL Server 2019 Express database. All five views and procedures executed, and no cross-database or cross-server dependencies remained. Demo login, dashboard and scanner pages, daily production, area production, and visibility endpoints were checked with the application.

Keep `.bak` files, operational records, and connection secrets outside version control. Refresh a portfolio database by reviewing schema changes and generating new synthetic data, never by exporting production rows.
