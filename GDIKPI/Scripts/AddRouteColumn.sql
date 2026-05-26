-- Agregar columna Route a la tabla Modules
ALTER TABLE [Modules] ADD [Route] varchar(255) NULL;

-- Actualizar los módulos existentes con sus rutas
UPDATE [Modules] SET [Route] = '/Quality/Defects' WHERE [ModuleName] = 'Registro de Defectos';
UPDATE [Modules] SET [Route] = '/Quality/Dashboard' WHERE [ModuleName] = 'Dashboard Quality';
UPDATE [Modules] SET [Route] = '/Reports/Production' WHERE [ModuleName] = 'Reportes de Producción';
UPDATE [Modules] SET [Route] = '/Admin/Users' WHERE [ModuleName] = 'Gestión de Usuarios';

-- Agregar más módulos con rutas específicas
INSERT INTO [Modules] ([ModuleName], [Description], [Route], [IsActive]) VALUES
('Gestión de Líneas', 'Administración de líneas de producción', '/Admin/ProductionLines', 1),
('Métricas OEE', 'Dashboard de métricas OEE', '/Analytics/OEE', 1),
('Ausentismo', 'Registro y control de ausentismo', '/HR/Absenteeism', 1),
('Configuración', 'Configuración del sistema', '/Settings/*', 1);

GO