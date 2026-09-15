-- Run on the newly initialized demo database; fails if its contracts are incomplete.
SET NOCOUNT ON;
IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) <> 28
    THROW 50010, 'Expected 28 application tables.', 1;
IF (SELECT COUNT(*) FROM sys.views WHERE is_ms_shipped = 0) <> 5
    THROW 50011, 'Expected five application views.', 1;
IF (SELECT COUNT(*) FROM sys.procedures WHERE is_ms_shipped = 0) <> 5
    THROW 50012, 'Expected five application procedures.', 1;
IF EXISTS (SELECT 1 FROM sys.sql_expression_dependencies
           WHERE referenced_database_name IS NOT NULL OR referenced_server_name IS NOT NULL)
    THROW 50013, 'Demo objects must not reference another database or server.', 1;
IF (SELECT COUNT(*) FROM dbo.ProductionData) <> 140
    THROW 50014, 'Expected 140 synthetic production intervals.', 1;
IF (SELECT COUNT(*) FROM dbo.ProductionOperatorsScans) <> 2520
    THROW 50015, 'Expected 2520 synthetic operator scans.', 1;
IF (SELECT COUNT(*) FROM dbo.ScannerProduction) <> 1680
    THROW 50016, 'Expected 1680 synthetic line scans.', 1;

SELECT COUNT(*) AS UserPermissionRows FROM dbo.vw_UserPermissions;
SELECT COUNT(*) AS ProductionLineRows FROM dbo.vw_ProductionLines;
SELECT COUNT(*) AS OeeRows FROM dbo.vw_OEE;
SELECT COUNT(*) AS RejectionRows FROM dbo.vw_Rejects;
SELECT COUNT(*) AS DefectRows FROM dbo.vw_DefectsManagement;

DECLARE @today date = CAST(GETDATE() AS date), @goal int;
EXEC dbo.GetDailyProduction @ProductionLinesID = 1, @TargetDate = @today;
EXEC dbo.GetGoalPiecesByHour @ProductionLinesID = 1, @TargetDate = @today,
    @TargetTime = '16:00', @GoalPieces = @goal OUTPUT;
IF @goal IS NULL OR @goal <= 0
    THROW 50017, 'Expected a positive calculated production goal.', 1;
EXEC dbo.GetDailyProductionByArea @AreaID = 1, @TargetDate = @today, @TargetTime = '16:00';
EXEC dbo.GetLineOEE @ProductionLineId = 1, @TargetDate = @today, @TargetTime = '16:00';
EXEC dbo.GetLineOEETEST @ProductionLineId = 1, @TargetDate = @today, @TargetTime = '16:00';
PRINT 'Demo database verification passed.';
GO
