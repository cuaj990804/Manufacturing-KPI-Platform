-- Public demo schema. No production records, users, logins, or server paths are exported.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE VIEW dbo.vw_UserPermissions
AS

SELECT u.EmployeeNumber,
       u.Name,
       a.CustomerName + ' - ' + a.AreaName AS Area,
       pl.LineNumber,
       STRING_AGG(p.Name, ', ') AS Permissions,
       a.AreaID,
       'Specific' AS AccessType
FROM dbo.UserLinePermissions AS ulp
INNER JOIN dbo.Users AS u ON ulp.EmployeeNumber = u.EmployeeNumber
INNER JOIN dbo.Permissions AS p ON ulp.PermissionID = p.PermissionID
INNER JOIN dbo.ProductionLines AS pl ON ulp.ProductionLinesID = pl.ProductionLinesID
INNER JOIN dbo.Areas AS a ON pl.AreaID = a.AreaID
GROUP BY u.EmployeeNumber, u.Name, a.CustomerName, a.AreaName, pl.LineNumber, a.AreaID

UNION ALL

SELECT u.EmployeeNumber,
       u.Name,
       a.CustomerName + ' - ' + a.AreaName AS Area,
       pl.LineNumber,
       'FULL ACCESS (ADMIN)' AS Permissions,
       a.AreaID,
       'Admin' AS AccessType
FROM dbo.Users AS u
CROSS JOIN dbo.ProductionLines AS pl
INNER JOIN dbo.Areas AS a ON pl.AreaID = a.AreaID
WHERE u.Role = 'admin'
GO
CREATE VIEW dbo.vw_ProductionLines
AS
SELECT a.CustomerName, a.AreaName, pl.LineNumber, pl.DailyGoal, pl.PersonalQuantity, u.Name AS SupervisorName, STRING_AGG(CONVERT(VARCHAR(5), b.BreakStart, 108) + ' - ' + CONVERT(VARCHAR(5), b.BreakEnd, 108), ', ')
                  AS Descansos
FROM     dbo.ProductionLines AS pl INNER JOIN
                  dbo.Areas AS a ON pl.AreaID = a.AreaID INNER JOIN
                  dbo.Breaks AS b ON pl.ProductionLinesID = b.ProductionLinesID LEFT OUTER JOIN
                  dbo.UserLinePermissions AS ulp ON pl.ProductionLinesID = ulp.ProductionLinesID AND ulp.PermissionID = 2 LEFT OUTER JOIN
                  dbo.Users AS u ON ulp.EmployeeNumber = u.EmployeeNumber
GROUP BY a.CustomerName, a.AreaName, pl.LineNumber, pl.DailyGoal, pl.PersonalQuantity, u.Name
GO
CREATE VIEW [dbo].[vw_OEE]
AS
WITH AllLines AS (
    SELECT
        pl.ProductionLinesID,
        pl.LineNumber,
        a.CustomerName + ' - ' + a.AreaName AS Area,
        CAST(GETDATE() AS DATE) AS ReportDate,
        'Sin Incidencias' AS Type,
        'N/A' AS Category,
        'N/A' AS Status,
        0 AS Quantity
    FROM dbo.ProductionLines AS pl
    INNER JOIN dbo.Areas AS a ON pl.AreaID = a.AreaID
),
IncidentData AS (
    SELECT
        ProductionLinesID,
        CAST(AbsenteeismDate AS DATE) AS ReportDate,
        'Ausentismo' AS Type,
        COALESCE(AbsenteeismCategory, 'Sin Categoría') AS Category,
        'Activo' AS Status,
        COALESCE(AbsenteeismQuantity, 0) AS Quantity
    FROM dbo.Absenteeism
    WHERE AbsenteeismDate IS NOT NULL
        AND ProductionLinesID IS NOT NULL

    UNION ALL

    SELECT
        ProductionLinesID,
        CAST(StartTime AS DATE) AS ReportDate,
        'Tiempo Muerto' AS Type,
        COALESCE(DowntimeCategory, 'Sin Categoría') AS Category,
        CASE
            WHEN Status = 'Active' THEN 'Activo'
            WHEN Status = 'Pending' THEN 'Pendiente'
            WHEN Status = 'Close' THEN 'Cerrado'
            WHEN Status = 'Open' THEN 'Abierto'
            ELSE COALESCE(Status, 'Pendiente')
        END AS Status,
        1 AS Quantity
    FROM dbo.DowntimeEvents
    WHERE StartTime IS NOT NULL
        AND ProductionLinesID IS NOT NULL
)
SELECT
    al.ProductionLinesID,
    al.LineNumber,
    al.Area,
    COALESCE(id.ReportDate, al.ReportDate) AS ReportDate,
    COALESCE(id.Type, al.Type) AS Type,
    COALESCE(id.Category, al.Category) AS Category,
    COALESCE(id.Status, al.Status) AS Status,
    SUM(COALESCE(id.Quantity, al.Quantity)) AS Total
FROM AllLines al
LEFT JOIN IncidentData id ON al.ProductionLinesID = id.ProductionLinesID
GROUP BY
    al.ProductionLinesID,
    al.LineNumber,
    al.Area,
    COALESCE(id.ReportDate, al.ReportDate),
    COALESCE(id.Type, al.Type),
    COALESCE(id.Category, al.Category),
    COALESCE(id.Status, al.Status);
GO
CREATE VIEW [dbo].[vw_Rejects]
AS
SELECT
    CAST(d.StartTime AS DATE) AS Fecha,
    DATEPART(ISO_WEEK, d.StartTime) AS NumeroSemana,
    r.Category,
    pl.LineNumber AS Linea,
    a.CustomerName + ' - ' + a.AreaName AS Area,
    def.DefectName,
    r.ProgramId,
    r.ProgramDescription,
    SUM(d.DefectQuantity) AS Cantidad
FROM dbo.Rejections AS r
INNER JOIN dbo.DefectsData AS d ON r.RejectionID = d.RejectionID
INNER JOIN dbo.ProductionLines AS pl ON d.ProductionLinesID = pl.ProductionLinesID
INNER JOIN dbo.Areas AS a ON pl.AreaID = a.AreaID
INNER JOIN dbo.Defects AS def ON d.DefectID = def.DefectID
GROUP BY
    CAST(d.StartTime AS DATE),
    DATEPART(ISO_WEEK, d.StartTime),
    r.Category,
    pl.LineNumber,
    a.CustomerName,
    a.AreaName,
    def.DefectName,
    r.ProgramId,
    r.ProgramDescription;
GO
CREATE VIEW dbo.vw_DefectsManagement
AS
SELECT dbo.Areas.CustomerName + ' - ' + dbo.Areas.AreaName AS CustomerArea, dbo.DefectsCategory.DefectCategoryName, dbo.Defects.DefectName
FROM     dbo.Defects INNER JOIN
                  dbo.DefectsCategory ON dbo.Defects.DefectCategoryID = dbo.DefectsCategory.DefectCategoryID INNER JOIN
                  dbo.Areas ON dbo.Defects.AreaID = dbo.Areas.AreaID
GO
CREATE PROCEDURE [dbo].[GetDailyProduction]
    @ProductionLinesID INT,
    @TargetDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @TargetDate IS NULL
        SET @TargetDate = CAST(GETDATE() AS DATE);

    DECLARE @LineNumber INT;
    DECLARE @DailyGoal INT;
    DECLARE @ShiftNumber INT;
    DECLARE @StartTime TIME;
    DECLARE @EndTime TIME;

    SELECT
        @LineNumber = LineNumber,
        @DailyGoal = DailyGoal,
        @ShiftNumber = ShiftNumber
    FROM dbo.ProductionLines
    WHERE ProductionLinesID = @ProductionLinesID;

    IF @LineNumber IS NULL
    BEGIN
        RAISERROR('Error: No se encontró la línea de producción con ID %d', 16, 1, @ProductionLinesID);
        RETURN;
    END

    SELECT
        @StartTime = StartTime,
        @EndTime = CASE
                      WHEN EndTime < StartTime THEN DATEADD(HOUR, 24, EndTime)
                      ELSE EndTime
                   END
    FROM dbo.Shifts
    WHERE ShiftNumber = @ShiftNumber;

    IF @StartTime IS NULL OR @EndTime IS NULL
    BEGIN
        RAISERROR('Error: Turno no encontrado para línea %d', 16, 1, @LineNumber);
        RETURN;
    END

    IF @DailyGoal <= 0
    BEGIN
        RAISERROR('Error: Meta diaria inválida en línea %d', 16, 1, @LineNumber);
        RETURN;
    END

    DECLARE @HourlyDistribution TABLE (
        HourSlot TIME,
        HourInterval VARCHAR(20),
        TotalMinutes INT,
        BreakMinutes INT,
        EffectiveMinutes INT,
        PiecesDecimal DECIMAL(10,4),
        PiecesAssigned INT,
        ProducedPieces INT,
        RejectedPieces INT,
        HourlyBalance DECIMAL(10,2),
        AccumulatedBalance DECIMAL(10,2),
        AccumulatedRejections INT,
        BreakInfo VARCHAR(50),
        SortOrder INT
    );

    DECLARE @CurrentHour TIME = @StartTime;
    DECLARE @SortCounter INT = 1;

    WHILE @CurrentHour < @EndTime
    BEGIN
        DECLARE @NextHour TIME = DATEADD(MINUTE, 60, @CurrentHour);
        IF @NextHour > @EndTime SET @NextHour = @EndTime;

        DECLARE @SegmentMinutes INT = DATEDIFF(MINUTE, @CurrentHour, @NextHour);
        DECLARE @BreakMinutes INT = 0;

        SELECT @BreakMinutes = ISNULL(SUM(
            DATEDIFF(MINUTE,
                CASE WHEN BreakStart > @CurrentHour THEN BreakStart ELSE @CurrentHour END,
                CASE WHEN BreakEnd < @NextHour THEN BreakEnd ELSE @NextHour END
            )), 0)
        FROM dbo.Breaks
        WHERE ProductionLinesID = @ProductionLinesID
          AND BreakEnd > @CurrentHour
          AND BreakStart < @NextHour;

        DECLARE @ProducedInInterval INT = 0;
        SELECT @ProducedInInterval = ISNULL(SUM(pd.ProducedPieces), 0)
        FROM dbo.ProductionData pd
        WHERE pd.ProductionLinesID = @ProductionLinesID
          AND pd.ProductionDate = @TargetDate
          AND pd.StartHour >= @CurrentHour
          AND pd.StartHour < @NextHour;

        DECLARE @RejectedInInterval INT = 0;
        SELECT @RejectedInInterval = ISNULL(SUM(r.DefectQuantity), 0)
        FROM dbo.Rejections r
        WHERE r.ProductionLinesID = @ProductionLinesID
          AND CAST(r.StartTime AS DATE) = @TargetDate
          AND CAST(r.StartTime AS TIME) >= @CurrentHour
          AND CAST(r.StartTime AS TIME) < @NextHour;

        INSERT INTO @HourlyDistribution (
            HourSlot,
            HourInterval,
            TotalMinutes,
            BreakMinutes,
            EffectiveMinutes,
            PiecesDecimal,
            PiecesAssigned,
            ProducedPieces,
            RejectedPieces,
            HourlyBalance,
            AccumulatedBalance,
            AccumulatedRejections,
            BreakInfo,
            SortOrder
        )
        VALUES (
            @CurrentHour,
            CONVERT(VARCHAR(5), @CurrentHour, 108) + ' - ' + CONVERT(VARCHAR(5), @NextHour, 108),
            @SegmentMinutes,
            @BreakMinutes,
            @SegmentMinutes - @BreakMinutes,
            0,
            0,
            @ProducedInInterval,
            @RejectedInInterval,
            0,
            0,
            0,
            CASE
                WHEN @BreakMinutes > 0 THEN 'Descanso (' + CAST(@BreakMinutes AS VARCHAR) + ' min)'
                ELSE 'Sin descanso'
            END,
            @SortCounter
        );

        SET @CurrentHour = @NextHour;
        SET @SortCounter = @SortCounter + 1;
    END;

    DECLARE @TotalEffectiveMinutes INT = (SELECT SUM(EffectiveMinutes) FROM @HourlyDistribution);

    IF @TotalEffectiveMinutes = 0
    BEGIN
        RAISERROR('Error: Minutos efectivos de producción son cero', 16, 1);
        RETURN;
    END;

    UPDATE @HourlyDistribution
    SET
        PiecesDecimal = CAST(@DailyGoal AS DECIMAL(10,4)) * EffectiveMinutes / @TotalEffectiveMinutes,
        PiecesAssigned = FLOOR(CAST(@DailyGoal AS DECIMAL(10,4)) * EffectiveMinutes / @TotalEffectiveMinutes);

    DECLARE @SumAssignedPieces INT = (SELECT SUM(PiecesAssigned) FROM @HourlyDistribution);

    DECLARE @FirstHourSlot TIME = (SELECT TOP 1 HourSlot FROM @HourlyDistribution ORDER BY SortOrder);
    DECLARE @TargetFullHourSlot TIME = (SELECT TOP 1 HourSlot FROM @HourlyDistribution
                                        WHERE EffectiveMinutes = 60 AND HourSlot != @FirstHourSlot
                                        ORDER BY SortOrder);

    IF @TargetFullHourSlot IS NOT NULL
    BEGIN

        UPDATE @HourlyDistribution
        SET PiecesAssigned = PiecesAssigned - 1
        WHERE HourSlot = @FirstHourSlot;

        UPDATE @HourlyDistribution
        SET PiecesAssigned = PiecesAssigned + 1
        WHERE HourSlot = @TargetFullHourSlot;
    END

    SET @SumAssignedPieces = (SELECT SUM(PiecesAssigned) FROM @HourlyDistribution);

    DECLARE @Difference INT = @DailyGoal - @SumAssignedPieces;
    IF @Difference > 0
    BEGIN

        WITH RankedByLowAssignment AS (
            SELECT
                HourSlot,
                ROW_NUMBER() OVER (
                    ORDER BY
                        CASE WHEN EffectiveMinutes = 60 THEN 0 ELSE 1 END,
                        PiecesAssigned,
                        SortOrder
                ) AS RowNum
            FROM @HourlyDistribution
            WHERE EffectiveMinutes > 0 AND HourSlot != @FirstHourSlot
        )
        UPDATE hd
        SET PiecesAssigned = PiecesAssigned + 1
        FROM @HourlyDistribution hd
        INNER JOIN RankedByLowAssignment rb ON hd.HourSlot = rb.HourSlot
        WHERE rb.RowNum <= @Difference;
    END
    ELSE IF @Difference < 0
    BEGIN

        WITH RankedIntervals AS (
            SELECT
                HourSlot,
                ROW_NUMBER() OVER (
                    ORDER BY
                        CASE WHEN EffectiveMinutes = 60 THEN 1 ELSE 0 END,
                        SortOrder DESC
                ) as RowNum
            FROM @HourlyDistribution
            WHERE EffectiveMinutes > 0 AND HourSlot != @FirstHourSlot AND PiecesAssigned > 0
        )
        UPDATE hd
        SET PiecesAssigned = PiecesAssigned - 1
        FROM @HourlyDistribution hd
        INNER JOIN RankedIntervals ri ON hd.HourSlot = ri.HourSlot
        WHERE ri.RowNum <= ABS(@Difference);
    END;

    UPDATE @HourlyDistribution
    SET HourlyBalance = CASE
        WHEN PiecesAssigned > 0 THEN ROUND(CAST(ProducedPieces AS DECIMAL(10,2)) / PiecesAssigned * 100, 2)
        ELSE 0.00
    END;

    WITH AccumulatedData AS (
        SELECT
            SortOrder,
            SUM(PiecesAssigned) OVER (ORDER BY SortOrder ROWS UNBOUNDED PRECEDING) AS AccGoal,
            SUM(ProducedPieces) OVER (ORDER BY SortOrder ROWS UNBOUNDED PRECEDING) AS AccProduced,
            SUM(RejectedPieces) OVER (ORDER BY SortOrder ROWS UNBOUNDED PRECEDING) AS AccRejected
        FROM @HourlyDistribution
    )
    UPDATE hd
    SET
        AccumulatedBalance = CASE
            WHEN ad.AccGoal > 0 THEN ROUND(CAST(ad.AccProduced AS DECIMAL(10,2)) / ad.AccGoal * 100, 2)
            ELSE 0.00
        END,
        AccumulatedRejections = ad.AccRejected
    FROM @HourlyDistribution hd
    INNER JOIN AccumulatedData ad ON hd.SortOrder = ad.SortOrder;

    WITH FinalResult AS (
        SELECT
            @LineNumber AS LineNumber,
            @TargetDate AS ProductionDate,
            HourInterval,
            PiecesAssigned AS GoalPieces,
            ProducedPieces,
            RejectedPieces,
            AccumulatedRejections,
            HourlyBalance,
            AccumulatedBalance,
            BreakInfo,
            'Detalle' AS RecordType,
            SortOrder,
            0 AS IsTotal
        FROM @HourlyDistribution

        UNION ALL

        SELECT
            @LineNumber AS LineNumber,
            @TargetDate AS ProductionDate,
            CONVERT(VARCHAR(5), @StartTime, 108) + ' - ' + CONVERT(VARCHAR(5), @EndTime, 108) AS HourInterval,
            @DailyGoal AS GoalPieces,
            (SELECT SUM(ProducedPieces) FROM @HourlyDistribution) AS ProducedPieces,
            (SELECT SUM(RejectedPieces) FROM @HourlyDistribution) AS RejectedPieces,
            (SELECT SUM(RejectedPieces) FROM @HourlyDistribution) AS AccumulatedRejections,
            CASE
                WHEN @DailyGoal > 0 THEN
                    ROUND(CAST((SELECT SUM(ProducedPieces) FROM @HourlyDistribution) AS DECIMAL(10,2)) / @DailyGoal * 100, 2)
                ELSE 0.00
            END AS HourlyBalance,
            CASE
                WHEN @DailyGoal > 0 THEN
                    ROUND(CAST((SELECT SUM(ProducedPieces) FROM @HourlyDistribution) AS DECIMAL(10,2)) / @DailyGoal * 100, 2)
                ELSE 0.00
            END AS AccumulatedBalance,
            'Total del turno' AS BreakInfo,
            'Total' AS RecordType,
            999 AS SortOrder,
            1 AS IsTotal
    )
    SELECT
        LineNumber,
        ProductionDate,
        HourInterval,
        GoalPieces,
        ProducedPieces,
        RejectedPieces,
        AccumulatedRejections,
        CAST(HourlyBalance AS INT) AS HourlyBalance,
        CAST(AccumulatedBalance AS INT) AS AccumulatedBalance,
        BreakInfo
    FROM FinalResult
    ORDER BY IsTotal, SortOrder;
END;
GO
CREATE PROCEDURE [dbo].[GetGoalPiecesByHour]
    @ProductionLinesID INT,
    @TargetDate DATE,
    @TargetTime TIME,
    @GoalPieces INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    CREATE TABLE #Temp (
        LineNumber INT,
        ProductionDate DATE,
        HourInterval VARCHAR(20),
        GoalPieces INT,
        ProducedPieces INT,
        RejectedPieces INT,
        AccumulatedRejections INT,
        HourlyBalance INT,
        AccumulatedBalance INT,
        BreakInfo VARCHAR(50)
    );

    INSERT INTO #Temp
    EXEC dbo.GetDailyProduction @ProductionLinesID, @TargetDate;

    DECLARE @AccumulatedGoal INT = 0;
    DECLARE @CurrentHourGoal INT = 0;
    DECLARE @InterpolatedPieces DECIMAL(10,2) = 0;
    DECLARE @MinutesIntoHour INT;
    DECLARE @HourStart TIME;
    DECLARE @HourEnd TIME;

    SELECT @AccumulatedGoal = ISNULL(SUM(GoalPieces), 0)
    FROM #Temp
    WHERE BreakInfo != 'Total del turno'
      AND TRY_CAST(RIGHT(HourInterval, 5) AS TIME) <= @TargetTime
      AND TRY_CAST(LEFT(HourInterval, 5) AS TIME) < @TargetTime
      AND TRY_CAST(RIGHT(HourInterval, 5) AS TIME) IS NOT NULL
      AND TRY_CAST(LEFT(HourInterval, 5) AS TIME) IS NOT NULL;

    SELECT TOP 1
        @CurrentHourGoal = GoalPieces,
        @HourStart = TRY_CAST(LEFT(HourInterval, 5) AS TIME),
        @HourEnd = TRY_CAST(RIGHT(HourInterval, 5) AS TIME)
    FROM #Temp
    WHERE BreakInfo != 'Total del turno'
      AND TRY_CAST(LEFT(HourInterval, 5) AS TIME) <= @TargetTime
      AND TRY_CAST(RIGHT(HourInterval, 5) AS TIME) > @TargetTime
      AND TRY_CAST(LEFT(HourInterval, 5) AS TIME) IS NOT NULL
      AND TRY_CAST(RIGHT(HourInterval, 5) AS TIME) IS NOT NULL;

    IF @CurrentHourGoal IS NOT NULL AND @HourStart IS NOT NULL AND @HourEnd IS NOT NULL
    BEGIN

        SET @MinutesIntoHour = DATEDIFF(MINUTE, @HourStart, @TargetTime);

        SET @InterpolatedPieces = (@CurrentHourGoal * @MinutesIntoHour) / 60.0;

        SET @GoalPieces = @AccumulatedGoal + CAST(ROUND(@InterpolatedPieces, 0) AS INT);
    END
    ELSE
    BEGIN

        SET @GoalPieces = @AccumulatedGoal;
    END

    IF @GoalPieces IS NULL
        SET @GoalPieces = 0;

    DROP TABLE #Temp;
END;
GO
CREATE PROCEDURE [dbo].[GetDailyProductionByArea]
    @AreaID INT,
    @TargetDate DATE = NULL,
    @TargetTime TIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @TargetDate IS NULL
        SET @TargetDate = CAST(GETDATE() AS DATE);

    IF @TargetTime IS NULL
        SET @TargetTime = CAST(GETDATE() AS TIME);

    DECLARE @RoundedTime TIME;

    IF DATEPART(MINUTE, @TargetTime) > 0 OR DATEPART(SECOND, @TargetTime) > 0
        SET @RoundedTime = CAST(DATEADD(HOUR, DATEPART(HOUR, @TargetTime) + 1, CAST('00:00:00' AS TIME)) AS TIME);
    ELSE
        SET @RoundedTime = @TargetTime;

    DECLARE @AreaName NVARCHAR(100),
            @CustomerName NVARCHAR(100),
            @AreaCustomerName NVARCHAR(200);

    SELECT
        @AreaName = AreaName,
        @CustomerName = CustomerName
    FROM dbo.Areas
    WHERE AreaID = @AreaID;

    SET @AreaCustomerName = @AreaName + ' ' + @CustomerName;

    CREATE TABLE #AllResults (
        AreaCustomerName NVARCHAR(200),
        LineNumber INT,
        ProductionDate DATE,
        HourInterval VARCHAR(20),
        GoalPieces INT,
        ProducedPieces INT,
        RejectedPieces INT,
        AccumulatedRejections INT,
        QualityPercentage DECIMAL(10,2),
        DowntimeMinutes INT,
        AccumulatedDowntime INT,
        AccumulatedBalance DECIMAL(10,2),
        EstimatedGoalPieces INT,
        RequirementGoalPieces INT,
        RequirementBalance DECIMAL(10,2)
    );

    DECLARE line_cursor CURSOR FOR
    SELECT ProductionLinesID
    FROM dbo.ProductionLines
    WHERE AreaID = @AreaID;

    DECLARE @ProductionLinesID INT;

    OPEN line_cursor;
    FETCH NEXT FROM line_cursor INTO @ProductionLinesID;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @LineNumber INT, @DailyGoal INT, @ShiftNumber INT;
        DECLARE @StartTime TIME, @EndTime TIME;

        SELECT @LineNumber = LineNumber, @DailyGoal = DailyGoal, @ShiftNumber = ShiftNumber
        FROM dbo.ProductionLines
        WHERE ProductionLinesID = @ProductionLinesID;

        IF @LineNumber IS NOT NULL
        BEGIN
            SELECT @StartTime = StartTime,
                   @EndTime = CASE WHEN EndTime < StartTime THEN DATEADD(HOUR, 24, EndTime) ELSE EndTime END
            FROM dbo.Shifts
            WHERE ShiftNumber = @ShiftNumber;

            IF @StartTime IS NOT NULL AND @EndTime IS NOT NULL AND @DailyGoal > 0
            BEGIN
                DECLARE @TotalProduced INT, @TotalRejected INT, @TotalDowntime INT;
                DECLARE @EstimatedGoalPieces INT, @RequirementGoalPieces INT;

                EXEC [dbo].[GetGoalPiecesByHour]
                    @ProductionLinesID = @ProductionLinesID,
                    @TargetDate = @TargetDate,
                    @TargetTime = @TargetTime,
                    @GoalPieces = @EstimatedGoalPieces OUTPUT;

                EXEC [dbo].[GetGoalPiecesByHour]
                    @ProductionLinesID = @ProductionLinesID,
                    @TargetDate = @TargetDate,
                    @TargetTime = @RoundedTime,
                    @GoalPieces = @RequirementGoalPieces OUTPUT;

                SELECT @TotalProduced = ISNULL(SUM(ProducedPieces), 0)
                FROM dbo.ProductionData
                WHERE ProductionLinesID = @ProductionLinesID AND ProductionDate = @TargetDate;

                SELECT @TotalRejected = ISNULL(SUM(DefectQuantity), 0)
                FROM dbo.Rejections
                WHERE ProductionLinesID = @ProductionLinesID AND CAST(StartTime AS DATE) = @TargetDate;

                SELECT @TotalDowntime = ISNULL(SUM(
                    DATEDIFF(MINUTE,
                        CASE WHEN CAST(StartTime AS TIME) < @StartTime THEN @StartTime ELSE CAST(StartTime AS TIME) END,
                        CASE WHEN EndTime IS NOT NULL THEN
                                CASE WHEN CAST(EndTime AS TIME) > @EndTime THEN @EndTime ELSE CAST(EndTime AS TIME) END
                            ELSE
                                CASE WHEN @TargetTime > @EndTime THEN @EndTime
                                     WHEN @TargetTime < @StartTime THEN @StartTime
                                     ELSE @TargetTime END
                        END
                    )
                ), 0)
                FROM dbo.DowntimeEvents
                WHERE ProductionLinesID = @ProductionLinesID AND CAST(StartTime AS DATE) = @TargetDate
                  AND ((EndTime IS NOT NULL AND CAST(EndTime AS TIME) > @StartTime AND CAST(StartTime AS TIME) < @EndTime)
                       OR (EndTime IS NULL AND CAST(StartTime AS TIME) >= @StartTime AND CAST(StartTime AS TIME) <= @TargetTime));

                INSERT INTO #AllResults
                SELECT @AreaCustomerName, @LineNumber, @TargetDate,
                    CONVERT(VARCHAR(5), @StartTime, 108) + ' - ' + CONVERT(VARCHAR(5), @EndTime, 108),
                    @DailyGoal, @TotalProduced, @TotalRejected, @TotalRejected,
                    CASE WHEN @TotalProduced > 0 THEN 100 - ROUND(CAST(@TotalRejected AS DECIMAL(10,2)) / @TotalProduced * 100, 2) ELSE 100 END,
                    @TotalDowntime, @TotalDowntime,
                    CASE WHEN @EstimatedGoalPieces > 0 THEN ROUND(CAST(@TotalProduced AS DECIMAL(10,2)) / @EstimatedGoalPieces * 100, 2) ELSE 0 END,
                    @EstimatedGoalPieces,
                    @RequirementGoalPieces,
                    CASE WHEN @RequirementGoalPieces > 0 THEN ROUND(CAST(@TotalProduced AS DECIMAL(10,2)) / @RequirementGoalPieces * 100, 2) ELSE 0 END;
            END
        END

        FETCH NEXT FROM line_cursor INTO @ProductionLinesID;
    END

    CLOSE line_cursor;
    DEALLOCATE line_cursor;

    SELECT * FROM #AllResults ORDER BY LineNumber;

    DROP TABLE #AllResults;
END;
GO
CREATE PROCEDURE [dbo].[GetLineOEETEST]
    @ProductionLineId INT,
    @TargetDate DATE,
    @TargetTime TIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LineNumber INT, @DailyGoal INT, @ShiftNumber INT, @AreaId INT;
    DECLARE @CustomerName NVARCHAR(255), @AreaName NVARCHAR(255);

    SELECT
        @LineNumber = pl.LineNumber,
        @DailyGoal = pl.DailyGoal,
        @ShiftNumber = pl.ShiftNumber,
        @AreaId = pl.AreaId,
        @CustomerName = a.CustomerName,
        @AreaName = a.AreaName
    FROM ProductionLines pl
    INNER JOIN Areas a ON pl.AreaId = a.AreaId
    WHERE pl.ProductionLinesId = @ProductionLineId;

    IF @LineNumber IS NULL
    BEGIN
        SELECT NULL AS LineNumber;
        RETURN;
    END

    DECLARE @PlannedMinutes INT;
    DECLARE @DowntimeMinutes INT;
    DECLARE @ProducedPieces INT;
    DECLARE @RejectedPieces INT;
    DECLARE @RequirementGoalPieces INT;
    DECLARE @OperatingMinutes INT;
    DECLARE @Availability DECIMAL(10,4);
    DECLARE @Performance DECIMAL(10,4);
    DECLARE @Quality DECIMAL(10,4);
    DECLARE @OEE DECIMAL(10,4);

    SET @PlannedMinutes = 0;
    SET @DowntimeMinutes = 0;
    SET @ProducedPieces = 0;
    SET @RejectedPieces = 0;
    SET @RequirementGoalPieces = 0;
    SET @Availability = 0;
    SET @Performance = 0;
    SET @Quality = 0;
    SET @OEE = 0;

    DECLARE @TargetDateTime DATETIME;
    SET @TargetDateTime = CAST(@TargetDate AS DATETIME) + CAST(@TargetTime AS DATETIME);

    DECLARE @StartTime TIME, @EndTime TIME;
    SELECT @StartTime = StartTime, @EndTime = EndTime
    FROM Shifts
    WHERE ShiftNumber = @ShiftNumber;

    IF @StartTime IS NOT NULL AND @EndTime IS NOT NULL
    BEGIN
        DECLARE @MinutesElapsed INT;
        SET @MinutesElapsed = DATEDIFF(MINUTE, @StartTime, @TargetTime);

        IF @MinutesElapsed < 0
            SET @MinutesElapsed = @MinutesElapsed + 1440;

        DECLARE @ElapsedBreakMinutes INT;
        SET @ElapsedBreakMinutes = 0;

        SELECT @ElapsedBreakMinutes = ISNULL(SUM(
            CASE
                WHEN BreakEnd <= @TargetTime THEN
                    CASE
                        WHEN BreakEnd >= BreakStart THEN DATEDIFF(MINUTE, BreakStart, BreakEnd)
                        ELSE DATEDIFF(MINUTE, BreakStart, BreakEnd) + 1440
                    END
                WHEN BreakStart <= @TargetTime THEN
                    DATEDIFF(MINUTE, BreakStart, @TargetTime)
                ELSE 0
            END
        ), 0)
        FROM Breaks
        WHERE ProductionLinesId = @ProductionLineId
            AND BreakStart IS NOT NULL
            AND BreakEnd IS NOT NULL;

        SET @PlannedMinutes = @MinutesElapsed - @ElapsedBreakMinutes;

        IF @PlannedMinutes < 0
            SET @PlannedMinutes = 0;
    END

    DECLARE @StartDateTime DATETIME;
    SET @StartDateTime = CAST(@TargetDate AS DATETIME);

    SELECT @DowntimeMinutes = ISNULL(SUM(
        DATEDIFF(MINUTE,
            CASE WHEN StartTime < @StartDateTime THEN @StartDateTime ELSE StartTime END,
            CASE
                WHEN EndTime IS NOT NULL THEN
                    CASE WHEN EndTime > @TargetDateTime THEN @TargetDateTime ELSE EndTime END
                WHEN Status = 'Open' THEN
                    CASE WHEN GETDATE() < @TargetDateTime THEN GETDATE() ELSE @TargetDateTime END
                ELSE StartTime
            END
        )
    ), 0)
    FROM DowntimeEvents
    WHERE ProductionLinesID = @ProductionLineId
        AND StartTime <= @TargetDateTime
        AND (EndTime >= @StartDateTime OR EndTime IS NULL OR Status = 'Open');

    IF @DowntimeMinutes < 0
        SET @DowntimeMinutes = 0;

    SELECT @ProducedPieces = ISNULL(SUM(ProducedPieces), 0)
    FROM ProductionData
    WHERE ProductionLinesId = @ProductionLineId
        AND ProductionDate = @TargetDate
        AND StartHour < @TargetTime;

    SELECT @RejectedPieces = ISNULL(SUM(DefectQuantity), 0)
    FROM Rejections
    WHERE ProductionLinesID = @ProductionLineId
        AND StartTime <= @TargetDateTime
        AND CAST(StartTime AS DATE) = @TargetDate;

    EXEC [dbo].[GetGoalPiecesByHour]
        @ProductionLinesID = @ProductionLineId,
        @TargetDate = @TargetDate,
        @TargetTime = @TargetTime,
        @GoalPieces = @RequirementGoalPieces OUTPUT;

    IF @RequirementGoalPieces IS NULL
        SET @RequirementGoalPieces = 0;

    SET @OperatingMinutes = @PlannedMinutes - @DowntimeMinutes;

    IF @OperatingMinutes < 0
        SET @OperatingMinutes = 0;

    IF @PlannedMinutes > 0 AND @OperatingMinutes > 0 AND @ProducedPieces > 0
    BEGIN
        DECLARE @PiecesPerMinute DECIMAL(10,4);
        SET @PiecesPerMinute = 0;

        IF @RequirementGoalPieces > 0 AND @PlannedMinutes > 0
            SET @PiecesPerMinute = (@RequirementGoalPieces * 1.0) / @PlannedMinutes;

        SET @Availability = (@OperatingMinutes * 1.0) / @PlannedMinutes;

        IF @PiecesPerMinute > 0 AND (@OperatingMinutes * @PiecesPerMinute) > 0
            SET @Performance = @ProducedPieces / (@OperatingMinutes * @PiecesPerMinute);

        IF @ProducedPieces > 0
            SET @Quality = (@ProducedPieces - @RejectedPieces) * 1.0 / @ProducedPieces;

        IF @Availability < 0 SET @Availability = 0;
        IF @Availability > 1 SET @Availability = 1;

        IF @Performance < 0 SET @Performance = 0;
        IF @Performance > 1 SET @Performance = 1;

        IF @Quality < 0 SET @Quality = 0;
        IF @Quality > 1 SET @Quality = 1;

        SET @OEE = @Availability * @Performance * @Quality;
    END

    SELECT
        @LineNumber AS LineNumber,
        @CustomerName + ' - ' + @AreaName AS AreaCustomerName,
        @RequirementGoalPieces AS RequirementGoalPieces,
        @ProducedPieces AS ProducedPieces,
        @DowntimeMinutes AS DowntimeMinutes,
        @RejectedPieces AS RejectedPieces,
        @Availability * 100 AS AvailabilityPercentage,
        @Performance * 100 AS PerformancePercentage,
        @Quality * 100 AS QualityPercentage,
        @OEE * 100 AS OeePercentage,
        @ProductionLineId AS ProductionLinesId,
        @PlannedMinutes AS PlannedMinutes,
        @OperatingMinutes AS OperatingMinutes;
END
GO
CREATE PROCEDURE [dbo].[GetLineOEE]
    @ProductionLineId INT,
    @TargetDate DATE,
    @TargetTime TIME
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LineNumber INT, @DailyGoal INT, @ShiftNumber INT, @AreaId INT;
    DECLARE @CustomerName NVARCHAR(255), @AreaName NVARCHAR(255);

    SELECT
        @LineNumber = pl.LineNumber,
        @DailyGoal = pl.DailyGoal,
        @ShiftNumber = pl.ShiftNumber,
        @AreaId = pl.AreaId,
        @CustomerName = a.CustomerName,
        @AreaName = a.AreaName
    FROM ProductionLines pl
    INNER JOIN Areas a ON pl.AreaId = a.AreaId
    WHERE pl.ProductionLinesId = @ProductionLineId;

    IF @LineNumber IS NULL
    BEGIN
        SELECT NULL AS LineNumber;
        RETURN;
    END

    DECLARE @PlannedMinutes INT;
    DECLARE @DowntimeMinutes INT;
    DECLARE @ProducedPieces INT;
    DECLARE @RejectedPieces INT;
    DECLARE @EstimatedRequirement INT;
    DECLARE @RequirementGoalPieces INT;
    DECLARE @OperatingMinutes INT;
    DECLARE @Availability DECIMAL(10,4);
    DECLARE @Performance DECIMAL(10,4);
    DECLARE @Quality DECIMAL(10,4);
    DECLARE @OEE DECIMAL(10,4);

    SET @PlannedMinutes = 0;
    SET @DowntimeMinutes = 0;
    SET @ProducedPieces = 0;
    SET @RejectedPieces = 0;
    SET @EstimatedRequirement = 0;
    SET @RequirementGoalPieces = 0;
    SET @Availability = 0;
    SET @Performance = 0;
    SET @Quality = 0;
    SET @OEE = 0;

    DECLARE @TargetDateTime DATETIME;
    SET @TargetDateTime = CAST(@TargetDate AS DATETIME) + CAST(@TargetTime AS DATETIME);

    DECLARE @RoundedTime TIME;

    IF DATEPART(MINUTE, @TargetTime) > 0 OR DATEPART(SECOND, @TargetTime) > 0
        SET @RoundedTime = CAST(DATEADD(HOUR, DATEPART(HOUR, @TargetTime) + 1, CAST('00:00:00' AS TIME)) AS TIME);
    ELSE
        SET @RoundedTime = @TargetTime;

    DECLARE @StartTime TIME, @EndTime TIME;
    SELECT @StartTime = StartTime, @EndTime = EndTime
    FROM Shifts
    WHERE ShiftNumber = @ShiftNumber;

    IF @StartTime IS NOT NULL AND @EndTime IS NOT NULL
    BEGIN

        DECLARE @TotalShiftMinutes INT;
        SET @TotalShiftMinutes = DATEDIFF(MINUTE, @StartTime, @EndTime);

        IF @TotalShiftMinutes <= 0
            SET @TotalShiftMinutes = @TotalShiftMinutes + 1440;

        DECLARE @EffectiveTime TIME;

        IF (@EndTime >= @StartTime AND @TargetTime > @EndTime) OR
           (@EndTime < @StartTime AND @TargetTime > @EndTime AND @TargetTime < @StartTime)
        BEGIN
            SET @EffectiveTime = @EndTime;
        END
        ELSE
        BEGIN
            SET @EffectiveTime = @TargetTime;
        END

        DECLARE @MinutesElapsed INT;
        SET @MinutesElapsed = DATEDIFF(MINUTE, @StartTime, @EffectiveTime);

        IF @MinutesElapsed < 0
            SET @MinutesElapsed = @MinutesElapsed + 1440;

        DECLARE @ElapsedBreakMinutes INT;
        SET @ElapsedBreakMinutes = 0;

        SELECT @ElapsedBreakMinutes = ISNULL(SUM(
            CASE
                WHEN BreakEnd <= @EffectiveTime THEN
                    CASE
                        WHEN BreakEnd >= BreakStart THEN DATEDIFF(MINUTE, BreakStart, BreakEnd)
                        ELSE DATEDIFF(MINUTE, BreakStart, BreakEnd) + 1440
                    END
                WHEN BreakStart <= @EffectiveTime THEN
                    DATEDIFF(MINUTE, BreakStart, @EffectiveTime)
                ELSE 0
            END
        ), 0)
        FROM Breaks
        WHERE ProductionLinesId = @ProductionLineId
            AND BreakStart IS NOT NULL
            AND BreakEnd IS NOT NULL;

        SET @PlannedMinutes = @MinutesElapsed - @ElapsedBreakMinutes;

        IF @PlannedMinutes < 0
            SET @PlannedMinutes = 0;

        DECLARE @MaxPlannedMinutes INT;
        SET @MaxPlannedMinutes = @TotalShiftMinutes - @ElapsedBreakMinutes;

        IF @MaxPlannedMinutes < 0
            SET @MaxPlannedMinutes = 0;

        IF @PlannedMinutes > @MaxPlannedMinutes
            SET @PlannedMinutes = @MaxPlannedMinutes;
    END

    DECLARE @StartDateTime DATETIME;
    SET @StartDateTime = CAST(@TargetDate AS DATETIME);

    SELECT @DowntimeMinutes = ISNULL(SUM(
        DATEDIFF(MINUTE,
            CASE WHEN StartTime < @StartDateTime THEN @StartDateTime ELSE StartTime END,
            CASE
                WHEN EndTime IS NOT NULL THEN
                    CASE WHEN EndTime > @TargetDateTime THEN @TargetDateTime ELSE EndTime END
                WHEN Status = 'Open' THEN
                    CASE WHEN GETDATE() < @TargetDateTime THEN GETDATE() ELSE @TargetDateTime END
                ELSE StartTime
            END
        )
    ), 0)
    FROM DowntimeEvents
    WHERE ProductionLinesID = @ProductionLineId
        AND StartTime <= @TargetDateTime
        AND (EndTime >= @StartDateTime OR EndTime IS NULL OR Status = 'Open');

    IF @DowntimeMinutes < 0
        SET @DowntimeMinutes = 0;

    SELECT @ProducedPieces = ISNULL(SUM(ProducedPieces), 0)
    FROM ProductionData
    WHERE ProductionLinesId = @ProductionLineId
        AND ProductionDate = @TargetDate
        AND StartHour < @TargetTime;

    SELECT @RejectedPieces = ISNULL(SUM(DefectQuantity), 0)
    FROM Rejections
    WHERE ProductionLinesID = @ProductionLineId
        AND StartTime <= @TargetDateTime
        AND CAST(StartTime AS DATE) = @TargetDate;

    EXEC [dbo].[GetGoalPiecesByHour]
        @ProductionLinesID = @ProductionLineId,
        @TargetDate = @TargetDate,
        @TargetTime = @TargetTime,
        @GoalPieces = @EstimatedRequirement OUTPUT;

    IF @EstimatedRequirement IS NULL
        SET @EstimatedRequirement = 0;

    EXEC [dbo].[GetGoalPiecesByHour]
        @ProductionLinesID = @ProductionLineId,
        @TargetDate = @TargetDate,
        @TargetTime = @RoundedTime,
        @GoalPieces = @RequirementGoalPieces OUTPUT;

    IF @RequirementGoalPieces IS NULL
        SET @RequirementGoalPieces = 0;

    SET @OperatingMinutes = @PlannedMinutes - @DowntimeMinutes;

    IF @OperatingMinutes < 0
        SET @OperatingMinutes = 0;

    IF @OperatingMinutes > @PlannedMinutes
        SET @OperatingMinutes = @PlannedMinutes;

    IF @PlannedMinutes > 0 AND @OperatingMinutes > 0 AND @ProducedPieces > 0
    BEGIN
        DECLARE @PiecesPerMinute DECIMAL(10,4);
        SET @PiecesPerMinute = 0;

        IF @RequirementGoalPieces > 0 AND @PlannedMinutes > 0
            SET @PiecesPerMinute = (@RequirementGoalPieces * 1.0) / @PlannedMinutes;

        SET @Availability = (@OperatingMinutes * 1.0) / @PlannedMinutes;

        IF @PiecesPerMinute > 0 AND (@OperatingMinutes * @PiecesPerMinute) > 0
            SET @Performance = @ProducedPieces / (@OperatingMinutes * @PiecesPerMinute);

        IF @ProducedPieces > 0
            SET @Quality = (@ProducedPieces - @RejectedPieces) * 1.0 / @ProducedPieces;

        IF @Availability < 0 SET @Availability = 0;
        IF @Availability > 1 SET @Availability = 1;

        IF @Performance < 0 SET @Performance = 0;
        IF @Performance > 1 SET @Performance = 1;

        IF @Quality < 0 SET @Quality = 0;
        IF @Quality > 1 SET @Quality = 1;

        SET @OEE = @Availability * @Performance * @Quality;
    END

    SELECT
        @LineNumber AS LineNumber,
        @CustomerName + ' - ' + @AreaName AS AreaCustomerName,
        @EstimatedRequirement AS EstimatedRequirement,
        @RequirementGoalPieces AS RequirementGoalPieces,
        @ProducedPieces AS ProducedPieces,
        @DowntimeMinutes AS DowntimeMinutes,
        @RejectedPieces AS RejectedPieces,
        @Availability * 100 AS AvailabilityPercentage,
        @Performance * 100 AS PerformancePercentage,
        @Quality * 100 AS QualityPercentage,
        @OEE * 100 AS OeePercentage,
        @ProductionLineId AS ProductionLinesId,
        @PlannedMinutes AS PlannedMinutes,
        @OperatingMinutes AS OperatingMinutes;
END
GO
