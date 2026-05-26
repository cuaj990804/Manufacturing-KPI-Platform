-- Script para verificar y agregar los permisos necesarios

-- Ver permisos existentes
SELECT PermissionID, Name, Description
FROM Permissions
ORDER BY PermissionID;

GO

-- Agregar permiso Create si no existe
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = 'Create')
BEGIN
    INSERT INTO Permissions (Name, Description)
    VALUES ('Create', 'Permiso para crear y editar registros');
    PRINT 'Permiso "Create" agregado';
END
ELSE
BEGIN
    PRINT 'El permiso "Create" ya existe';
END

GO

-- Agregar permiso View si no existe
IF NOT EXISTS (SELECT 1 FROM Permissions WHERE Name = 'View')
BEGIN
    INSERT INTO Permissions (Name, Description)
    VALUES ('View', 'Permiso para ver y visualizar información');
    PRINT 'Permiso "View" agregado';
END
ELSE
BEGIN
    PRINT 'El permiso "View" ya existe';
END

GO

-- Mostrar permisos finales
SELECT PermissionID, Name, Description
FROM Permissions
ORDER BY Name;

GO
