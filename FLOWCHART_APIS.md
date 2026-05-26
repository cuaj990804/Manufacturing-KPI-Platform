# API Flow Diagram

This diagram shows the main APIs that send information into the system, the external dependencies the application consults, and how the data ends up in the database and real-time dashboards.

## General flow

```mermaid
flowchart TD
    U[User / Operator / Admin] --> WEB[Web UI MVC + JS]

    WEB --> PD[ProductionDataApiController]
    WEB --> SC[ScannerProductionApiController]
    WEB --> DF[DefectsApiController]
    WEB --> DT[DownTimeApiController]
    WEB --> AB[AbsenteeismApiController]
    WEB --> AR[AreaApiController]
    WEB --> PC[PanelControlApiController]
    WEB --> AS[AttendanceLineSyncApiController]

    EXT[External Part Number / Program API] --> SC
    EXT2[External Attendance Source] --> AS

    PD --> DB[(SQL Server / KPIS)]
    SC --> DB
    DF --> DB
    DT --> DB
    AB --> DB
    AR --> DB
    PC --> DB
    AS --> DB

    PD --> AUD[AuditService]
    DF --> AUD
    DT --> AUD
    AB --> AUD
    AR --> AUD
    AUD --> DB

    PD --> HUB[SignalR DashboardHub]
    SC --> HUB
    DF --> HUB
    HUB --> DASH[Area dashboards]
    HUB --> LINE[Line scanner view]
```

## APIs that input data into the system

### 1. Manual production entry

```mermaid
flowchart LR
    A[Production Form] --> B[POST /api/ProductionDataApi]
    A --> C[PUT /api/ProductionDataApi/{id}]
    A --> D[POST /api/ProductionDataApi/UpdateProducedPieces]
    B --> E[(ProductionData)]
    C --> E
    D --> E
    B --> F[(AuditLog)]
    C --> F
    D --> F
    B --> G[SignalR RequirementUpdated]
    C --> G
    D --> G
```

### 2. Production scanner

```mermaid
flowchart LR
    A[Scanner UI] --> B[GET /api/ScannerProductionApi/GetPartNumber]
    A --> C[GET /api/ScannerProductionApi/GetPrograms]
    A --> D[POST /api/ScannerProductionApi/SaveScan]
    A --> E[POST /api/ScannerProductionApi/SaveRejection]
    A --> F[DELETE /api/ScannerProductionApi/DeleteLastScan]

    X[External API] --> B
    X --> C

    D --> G[(ScannerProduction)]
    D --> H[(ProductionData)]
    E --> I[(Rejections)]
    E --> J[(DefectsData)]
    E --> H
    F --> G
    F --> H

    D --> K[SignalR ProductionDataUpdated]
    E --> K
    D --> L[SignalR UpdateLineMetrics]
    E --> L
```

### 3. Defects / quality registration

```mermaid
flowchart LR
    A[Quality Module] --> B[POST /api/DefectsApi/register]
    A --> C[PUT /api/DefectsApi/update]
    B --> D[(Rejections)]
    B --> E[(DefectsData)]
    C --> D
    C --> E
    B --> F[(AuditLog)]
    C --> F
    B --> G[SignalR RejectDataUpdated]
    C --> G
```

### 4. Downtime

```mermaid
flowchart LR
    A[Downtime Module] --> B[POST /api/DownTimeApi/register]
    A --> C[POST /api/DownTimeApi/close]
    B --> D[(DowntimeEvents)]
    C --> D
    B --> E[(AuditLog)]
    C --> E
```

### 5. Absenteeism

```mermaid
flowchart LR
    A[Absenteeism Module] --> B[POST /api/AbsenteeismApi/AbsenteeismCreate]
    B --> C[(Absenteeism)]
    B --> D[(AuditLog)]
```

### 6. Area and line setup

```mermaid
flowchart LR
    A[Configuration Panel] --> B[POST /api/AreaApi/CreateArea]
    A --> C[POST /api/AreaApi/CreateProductionLine]
    A --> D[POST /api/AreaApi/UpdateArea]
    A --> E[PUT /api/AreaApi/UpdateProductionLine]

    B --> F[(Areas)]
    C --> G[(ProductionLines)]
    C --> H[(Breaks)]
    C --> I[(UserLinePermissions)]
    C --> J[(UserAreaPermissions)]
    D --> F
    E --> G
    E --> H
    E --> I
    E --> J
    E --> K[(AuditLog)]
```

### 7. Attendance synchronization

```mermaid
flowchart LR
    A[Admin / Sync Job] --> B[POST /api/AttendanceLineSyncApi/sync]
    X[External Attendance Source] --> B
    B --> C[AttendanceLineSyncService]
    C --> D[(Attendance tables / line mappings)]
```

## Summary of the most important input endpoints

| API | Endpoint | Input type | Main destination |
| --- | --- | --- | --- |
| `ProductionDataApiController` | `POST /api/ProductionDataApi` | Manual production entry | `ProductionData`, auditing, SignalR |
| `ProductionDataApiController` | `PUT /api/ProductionDataApi/{id}` | Manual update | `ProductionData`, auditing, SignalR |
| `ProductionDataApiController` | `POST /api/ProductionDataApi/UpdateProducedPieces` | Quick piece adjustment | `ProductionData`, auditing, SignalR |
| `ScannerProductionApiController` | `POST /api/ScannerProductionApi/SaveScan` | Piece scan | `ScannerProduction`, `ProductionData`, SignalR |
| `ScannerProductionApiController` | `POST /api/ScannerProductionApi/SaveRejection` | Rejection from scanner | `Rejections`, `DefectsData`, `ProductionData`, SignalR |
| `DefectsApiController` | `POST /api/DefectsApi/register` | Defect capture | `Rejections`, `DefectsData`, auditing, SignalR |
| `DefectsApiController` | `PUT /api/DefectsApi/update` | Defect editing | `Rejections`, `DefectsData`, auditing, SignalR |
| `DownTimeApiController` | `POST /api/DownTimeApi/register` | Downtime start | `DowntimeEvents`, auditing |
| `DownTimeApiController` | `POST /api/DownTimeApi/close` | Downtime close | `DowntimeEvents`, auditing |
| `AbsenteeismApiController` | `POST /api/AbsenteeismApi/AbsenteeismCreate` | Absenteeism entry | `Absenteeism`, auditing |
| `AreaApiController` | `POST /api/AreaApi/CreateArea` | Area creation | `Areas` |
| `AreaApiController` | `POST /api/AreaApi/CreateProductionLine` | Line creation | `ProductionLines`, `Breaks`, permissions |
| `AreaApiController` | `PUT /api/AreaApi/UpdateProductionLine` | Line editing | lines, breaks, permissions, auditing |
| `AttendanceLineSyncApiController` | `POST /api/AttendanceLineSyncApi/sync` | External synchronization | attendance service + database |

## Recommended usage

- If you want to show this on GitHub, link this file from `README.md`.
- If you want to present it, export only the first diagram and the `ScannerProductionApiController` flow, because that is the most complete operational path in the system.
