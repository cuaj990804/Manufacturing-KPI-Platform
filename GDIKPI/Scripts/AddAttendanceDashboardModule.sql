IF NOT EXISTS (
    SELECT 1
    FROM [Modules]
    WHERE [ModuleName] = 'Dashboard Asistencia'
       OR [Route] = '/AttendanceDashboard'
)
BEGIN
    INSERT INTO [Modules] ([ModuleName], [Description], [Route], [IsActive])
    VALUES ('Dashboard Asistencia', 'Consulta de asistencia y ausencias por departamento', '/AttendanceDashboard', 1);
END
ELSE
BEGIN
    UPDATE [Modules]
    SET [Route] = '/AttendanceDashboard',
        [Description] = 'Consulta de asistencia y ausencias por departamento',
        [IsActive] = 1
    WHERE [ModuleName] = 'Dashboard Asistencia'
       OR [Route] = '/AttendanceDashboard';
END
