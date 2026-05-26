-- Script para agregar el módulo "Registro de Producción"

-- Verificar si el módulo ya existe
IF NOT EXISTS (SELECT 1 FROM [Modules] WHERE [ModuleName] = 'Registro de Producción')
BEGIN
    INSERT INTO [Modules] ([ModuleName], [Description], [Route], [IsActive])
    VALUES ('Registro de Producción', 'Módulo para registrar datos de producción', '/ProductionData/Create', 1);

    PRINT 'Módulo "Registro de Producción" agregado exitosamente';
END
ELSE
BEGIN
    PRINT 'El módulo "Registro de Producción" ya existe';
END

GO

-- Mostrar los módulos existentes
SELECT ModuleID, ModuleName, Description, Route, IsActive
FROM [Modules]
ORDER BY ModuleName;

GO
