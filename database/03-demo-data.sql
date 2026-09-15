-- Entirely synthetic demonstration data. No production rows are copied.
-- Run once after 01-schema.sql and 02-views-and-procedures.sql.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() IN ('master', 'model', 'msdb', 'tempdb')
    THROW 50001, 'Select the demo database first.', 1;
IF EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.partitions p ON p.object_id = t.object_id
    WHERE t.is_ms_shipped = 0 AND p.index_id IN (0, 1) AND p.rows > 0
)
    THROW 50002, 'Demo data requires an empty application schema.', 1;

BEGIN TRANSACTION;

INSERT dbo.Shifts (ShiftNumber, StartTime, EndTime) VALUES (1, '07:00', '17:00');
SET IDENTITY_INSERT dbo.Areas ON;
INSERT dbo.Areas (AreaID, AreaName, CustomerName)
VALUES (1, N'Demo Assembly', N'Demo Customer A'), (2, N'Demo Finishing', N'Demo Customer B');
SET IDENTITY_INSERT dbo.Areas OFF;

SET IDENTITY_INSERT dbo.ProductionLines ON;
INSERT dbo.ProductionLines
    (ProductionLinesID, LineNumber, DailyGoal, PersonalQuantity, ShiftNumber, AreaID, IsActive, LineName, StandardTime)
VALUES (1, 101, 500, 2, 1, 1, 1, N'Demo Line A', 0.04),
       (2, 102, 450, 2, 1, 2, 1, N'Demo Line B', 0.04);
SET IDENTITY_INSERT dbo.ProductionLines OFF;
INSERT dbo.Breaks (ProductionLinesID, BreakStart, BreakEnd)
VALUES (1, '12:00', '12:30'), (2, '12:00', '12:30');

INSERT dbo.Users (EmployeeNumber, Name, Role, Area)
VALUES ('900001', 'Demo Administrator', 'Admin', N'Demo Assembly'),
       ('900002', 'Demo Supervisor', 'Supervisor', N'Demo Assembly');

INSERT dbo.Permissions (Name, Description)
VALUES ('View', 'View demo information'), ('Create', 'Create demo records'),
       ('Edit', 'Edit demo records'), ('Delete', 'Delete demo records');

INSERT dbo.Modules (ModuleName, Description, IsActive, Route)
VALUES ('Production', 'Demo production entry', 1, '/ProductionData/Index'),
       ('Dashboard Production', 'Demo production dashboard', 1, '/DashboardProduction/Index'),
       ('Production Operators', 'Demo operator management', 1, '/ProductionOperators/Index'),
       ('Production Operators Dashboard', 'Demo operator statistics', 1, '/ProductionOperatorsDashboard/Index'),
       ('Dashboard Quality', 'Demo quality dashboard', 1, '/QualityDashboard/Index'),
       ('Dashboard Efficiency', 'Demo efficiency dashboard', 1, '/Efficiency/Index'),
       ('OEE', 'Demo OEE dashboard', 1, '/OEE/Index'),
       ('Panel de Control', 'Demo configuration', 1, '/PanelControl/Index');

INSERT dbo.UserModulePermissions (EmployeeNumber, ModuleID, PermissionID)
SELECT '900001', m.ModuleID, p.PermissionID FROM dbo.Modules m CROSS JOIN dbo.Permissions p;
INSERT dbo.UserAreaPermissions (EmployeeNumber, AreaID, PermissionID)
SELECT '900001', a.AreaID, p.PermissionID FROM dbo.Areas a CROSS JOIN dbo.Permissions p;
INSERT dbo.UserLinePermissions (EmployeeNumber, ProductionLinesID, PermissionID)
SELECT '900001', l.ProductionLinesID, p.PermissionID FROM dbo.ProductionLines l CROSS JOIN dbo.Permissions p;
INSERT dbo.UserModulePermissions (EmployeeNumber, ModuleID, PermissionID)
SELECT '900002', m.ModuleID, p.PermissionID FROM dbo.Modules m CROSS JOIN dbo.Permissions p WHERE p.Name = 'View';
INSERT dbo.UserAreaPermissions (EmployeeNumber, AreaID, PermissionID)
SELECT '900002', 1, PermissionID FROM dbo.Permissions WHERE Name = 'View';
INSERT dbo.UserLinePermissions (EmployeeNumber, ProductionLinesID, PermissionID)
SELECT '900002', 1, PermissionID FROM dbo.Permissions WHERE Name = 'View';

SET IDENTITY_INSERT dbo.ProductionOperators ON;
INSERT dbo.ProductionOperators
    (OperatorId, EmployeeNumber, NameOperator, LastnameOperator, AreaId, Operation, Goal, Active, ProductionLinesID)
VALUES (1, 900101, 'Demo', 'Operator A', 1, 'Assembly', 50, 1, 1),
       (2, 900102, 'Demo', 'Operator B', 1, 'Inspection', 45, 1, 1),
       (3, 900103, 'Demo', 'Operator C', 2, 'Finishing', 45, 1, 2);
SET IDENTITY_INSERT dbo.ProductionOperators OFF;

SET IDENTITY_INSERT dbo.DefectsCategory ON;
INSERT dbo.DefectsCategory (DefectCategoryID, DefectCategoryName) VALUES (1, N'Demo visual inspection');
SET IDENTITY_INSERT dbo.DefectsCategory OFF;
SET IDENTITY_INSERT dbo.Defects ON;
INSERT dbo.Defects (DefectID, DefectName, AreaID, DefectCategoryID)
VALUES (1, N'Demo surface mark', 1, 1), (2, N'Demo alignment issue', 2, 1);
SET IDENTITY_INSERT dbo.Defects OFF;

DECLARE @today date = CAST(GETDATE() AS date);
DECLARE @day int = 0;
WHILE @day < 7
BEGIN
    DECLARE @date date = DATEADD(day, -@day, @today);
    DECLARE @hour int = 7;
    WHILE @hour < 17
    BEGIN
        INSERT dbo.ProductionData
            (ProductionLinesID, ProductionDate, StartHour, EndHour, ProducedPieces, RejectedPieces, ProgramId, ProgramDescription)
        SELECT ProductionLinesID, @date, TIMEFROMPARTS(@hour, 0, 0, 0, 0),
               TIMEFROMPARTS(@hour + 1, 0, 0, 0, 0),
               42 + ((@day + @hour + ProductionLinesID) % 12), 1 + (@hour % 3),
               1000 + ProductionLinesID, 'Synthetic demo program'
        FROM dbo.ProductionLines;

        DECLARE @scan int = 1;
        WHILE @scan <= 12
        BEGIN
            DECLARE @timestamp datetime = DATEADD(minute, @scan * 4, DATEADD(hour, @hour, CAST(@date AS datetime)));
            INSERT dbo.ProductionOperatorsScans (OperatorId, Code, ScannedAt)
            SELECT OperatorId, CONCAT('DEMO-', OperatorId, '-', @day, '-', @hour, '-', @scan), @timestamp
            FROM dbo.ProductionOperators;
            INSERT dbo.ScannerProduction (ScannerProductionDateTime, LineId, PartNumber, ScannerValue)
            SELECT @timestamp, ProductionLinesID, CONCAT('DEMO-PART-', ProductionLinesID),
                   CONCAT('DEMO-L', ProductionLinesID, '-', @day, '-', @hour, '-', @scan)
            FROM dbo.ProductionLines;
            SET @scan += 1;
        END;
        SET @hour += 1;
    END;

    INSERT dbo.Rejections
        (ProductionLinesID, Category, EmployeeNumber, DefectQuantity, StartTime, EndTime, ProgramId, ProgramDescription)
    VALUES (1, 'Demo visual inspection', '900002', 3,
            DATEADD(hour, 9, CAST(@date AS datetime)), DATEADD(hour, 10, CAST(@date AS datetime)),
            1001, N'Synthetic demo program');
    DECLARE @rejection int = SCOPE_IDENTITY();
    INSERT dbo.DefectsData
        (RejectionID, ProductionLinesID, Category, EmployeeNumber, DefectID, DefectQuantity, StartTime, EndTime, ScannerValue)
    VALUES (@rejection, 1, 'Demo visual inspection', '900002', 1, 3,
            DATEADD(hour, 9, CAST(@date AS datetime)), DATEADD(hour, 10, CAST(@date AS datetime)), CONCAT('DEMO-REJECT-', @day));
    SET @day += 1;
END;

COMMIT TRANSACTION;
PRINT 'Synthetic demo loaded. Sign in as 900001 (administrator) or 900002 (supervisor).';
GO
