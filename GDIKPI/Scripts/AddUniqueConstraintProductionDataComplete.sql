-- Script para agregar constraint único que evite registros duplicados de producción
-- Esto previene duplicados a nivel de base de datos

-- Primero verificar si ya existe el constraint
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductionData_Unique_Record')
BEGIN
    -- Crear índice único compuesto para evitar duplicados
    CREATE UNIQUE NONCLUSTERED INDEX IX_ProductionData_Unique_Record
    ON ProductionData (ProductionLinesId, ProductionDate, StartHour, EndHour, ProgramId)
    WHERE ProgramId IS NOT NULL;

    PRINT 'Índice único creado exitosamente para ProductionData';
END
ELSE
BEGIN
    PRINT 'El índice único ya existe para ProductionData';
END

-- También agregar constraint único para casos donde ProgramId es NULL
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductionData_Unique_Record_NullProgram')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_ProductionData_Unique_Record_NullProgram
    ON ProductionData (ProductionLinesId, ProductionDate, StartHour, EndHour)
    WHERE ProgramId IS NULL;

    PRINT 'Índice único para ProgramId NULL creado exitosamente';
END
ELSE
BEGIN
    PRINT 'El índice único para ProgramId NULL ya existe';
END