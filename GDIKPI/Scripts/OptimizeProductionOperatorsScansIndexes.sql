IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProductionOperatorsScans_Code_OperatorId_ScannedAt'
      AND object_id = OBJECT_ID('dbo.ProductionOperatorsScans')
)
BEGIN
    CREATE INDEX IX_ProductionOperatorsScans_Code_OperatorId_ScannedAt
    ON dbo.ProductionOperatorsScans (Code, OperatorId, ScannedAt DESC);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProductionOperators_Operation_OperatorId'
      AND object_id = OBJECT_ID('dbo.ProductionOperators')
)
BEGIN
    CREATE INDEX IX_ProductionOperators_Operation_OperatorId
    ON dbo.ProductionOperators (Operation, OperatorId);
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProductionOperatorsScans_ScannedAt'
      AND object_id = OBJECT_ID('dbo.ProductionOperatorsScans')
)
BEGIN
    CREATE INDEX IX_ProductionOperatorsScans_ScannedAt
    ON dbo.ProductionOperatorsScans (ScannedAt DESC)
    INCLUDE (OperatorId, Code);
END;
