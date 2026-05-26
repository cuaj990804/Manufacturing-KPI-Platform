IF NOT EXISTS (
    SELECT 1
    FROM [Modules]
    WHERE [ModuleName] IN ('Dashboard Eficiencia', 'Dashboard Efficiency')
       OR [Route] = '/Efficiency'
)
BEGIN
    INSERT INTO [Modules] ([ModuleName], [Description], [Route], [IsActive])
    VALUES ('Dashboard Eficiencia', 'Dashboard de métricas de eficiencia', '/Efficiency', 1);
END
ELSE
BEGIN
    UPDATE [Modules]
    SET [Route] = COALESCE([Route], '/Efficiency'),
        [IsActive] = 1
    WHERE [ModuleName] IN ('Dashboard Eficiencia', 'Dashboard Efficiency')
       OR [Route] = '/Efficiency';
END

GO
