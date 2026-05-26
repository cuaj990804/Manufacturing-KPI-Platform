-- Script para agregar constraint único para prevenir registros duplicados de producción
-- Este constraint evita que se inserten registros con la misma combinación de:
-- Línea, Fecha, Hora Inicio, Hora Fin y Programa

USE KPIS;
GO

-- Verificar si ya existe el constraint
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductionData_Unique_Record')
BEGIN
    -- Crear índice único compuesto
    CREATE UNIQUE NONCLUSTERED INDEX IX_ProductionData_Unique_Record
    ON ProductionData (ProductionLinesId, ProductionDate, StartHour, EndHour, ProgramId)
    WHERE ProgramId IS NOT NULL;

    PRINT 'Índice único creado exitosamente para prevenir registros duplicados.';
END
ELSE
BEGIN
    PRINT 'El índice único ya existe.';
END
GO

-- Verificar registros duplicados existentes antes de aplicar el constraint
SELECT
    ProductionLinesId,
    ProductionDate,
    StartHour,
    EndHour,
    ProgramId,
    COUNT(*) as Duplicados
FROM ProductionData
WHERE ProgramId IS NOT NULL
GROUP BY ProductionLinesId, ProductionDate, StartHour, EndHour, ProgramId
HAVING COUNT(*) > 1
ORDER BY Duplicados DESC;

-- Si existen duplicados, este script los mostrará para que puedan ser limpiados manualmente
-- antes de aplicar el constraint único