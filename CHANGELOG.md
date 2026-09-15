# Changelog

## 2026-09-15 — Production dashboards and line scanning

### Features

- Configure production dashboard card visibility and ordering, and broadcast changes to connected clients.
- Add a production-line scanner with line validation, recent activity, batch quantities, inactivity timeout, and synchronized feedback.
- Associate production operators with production lines and expand operator dashboard reporting and exports.
- Add configurable scanner prefix and suffix validation.

### Public configuration and documentation

- Refresh application source while preserving the public repository's history and portfolio screenshots.
- Use local SQL Server and external-service examples instead of private environment settings.
- Read the part-number API base URL from configuration across the affected controllers.
- Exclude employee seed records, runtime dashboard preferences, and local tooling artifacts.
- Clarify the relationship to GDIKPI and document setup requirements and current limitations.

### Database upgrade notes

These scripts modify an existing compatible development schema; they do not create the complete application database.

1. Review and apply `GDIKPI/Scripts/AddProductionOperatorsProductionLine.sql` to add the operator-to-line relationship to an existing `ProductionOperators` table.
2. Review and apply `GDIKPI/Scripts/CreateDashboardProductionCardSettings.sql` to create dashboard card settings.
3. `GDIKPI/Scripts/AddProductionOperators.sql` defines operator tables for environments where they do not already exist. Do not run it over existing tables. Employee seed data is intentionally omitted; use synthetic records for demonstrations.

Validate database upgrades and external integrations in a development environment before deploying.
