IF NOT EXISTS (
    SELECT 1
    FROM [Modules]
    WHERE [ModuleName] IN ('Dashboard Escaneos', 'Production Operators Dashboard')
       OR [Route] = '/ProductionOperatorsDashboard'
)
BEGIN
    INSERT INTO [Modules] ([ModuleName], [Description], [Route], [IsActive])
    VALUES ('Dashboard Escaneos', 'Visualizacion de escaneos de operadores', '/ProductionOperatorsDashboard', 1);
END
ELSE
BEGIN
    UPDATE [Modules]
    SET [Route] = '/ProductionOperatorsDashboard',
        [Description] = 'Visualizacion de escaneos de operadores',
        [IsActive] = 1
    WHERE [ModuleName] IN ('Dashboard Escaneos', 'Production Operators Dashboard')
       OR [Route] = '/ProductionOperatorsDashboard';
END;
