-- Script para crear las tablas Module y UserModulePermission

-- Crear tabla Modules
CREATE TABLE [Modules] (
    [ModuleID] int NOT NULL IDENTITY,
    [ModuleName] varchar(255) NOT NULL,
    [Description] varchar(255) NULL,
    [IsActive] bit NOT NULL DEFAULT 1,
    CONSTRAINT [PK__Module__2B7477E7] PRIMARY KEY ([ModuleID])
);

-- Crear tabla UserModulePermissions
CREATE TABLE [UserModulePermissions] (
    [UserModulePermissionID] int NOT NULL IDENTITY,
    [EmployeeNumber] int NOT NULL,
    [ModuleID] int NOT NULL,
    [PermissionID] int NOT NULL,
    CONSTRAINT [PK__UserModule__3F6E3CEA] PRIMARY KEY ([UserModulePermissionID]),
    CONSTRAINT [UserModulePermissions_Modules] FOREIGN KEY ([ModuleID]) REFERENCES [Modules] ([ModuleID]),
    CONSTRAINT [UserModulePermissions_Users] FOREIGN KEY ([EmployeeNumber]) REFERENCES [Users] ([EmployeeNumber]),
    CONSTRAINT [UserModulePermissions_Permissions] FOREIGN KEY ([PermissionID]) REFERENCES [Permissions] ([PermissionID])
);

-- Insertar módulos de ejemplo
INSERT INTO [Modules] ([ModuleName], [Description], [IsActive]) VALUES
('Registro de Defectos', 'Módulo para registrar y gestionar defectos de calidad', 1),
('Dashboard Quality', 'Panel de control para supervisores de calidad', 1),
('Reportes de Producción', 'Módulo de reportes de producción', 1),
('Gestión de Usuarios', 'Módulo para administrar usuarios del sistema', 1);

-- Insertar permisos de ejemplo si no existen
IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = 'View')
    INSERT INTO [Permissions] ([Name], [Description]) VALUES ('View', 'Permiso para ver información');

IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = 'Create')
    INSERT INTO [Permissions] ([Name], [Description]) VALUES ('Create', 'Permiso para crear registros');

IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = 'Edit')
    INSERT INTO [Permissions] ([Name], [Description]) VALUES ('Edit', 'Permiso para editar registros');

IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Name] = 'Delete')
    INSERT INTO [Permissions] ([Name], [Description]) VALUES ('Delete', 'Permiso para eliminar registros');

GO