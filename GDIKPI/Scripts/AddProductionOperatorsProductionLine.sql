IF COL_LENGTH('dbo.ProductionOperators', 'ProductionLinesID') IS NULL
BEGIN
    ALTER TABLE dbo.ProductionOperators
    ADD ProductionLinesID INT NULL;
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_ProductionOperators_ProductionLines'
)
BEGIN
    ALTER TABLE dbo.ProductionOperators
    ADD CONSTRAINT FK_ProductionOperators_ProductionLines
        FOREIGN KEY (ProductionLinesID)
        REFERENCES dbo.ProductionLines(ProductionLinesID);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ProductionOperators_ProductionLinesID'
      AND object_id = OBJECT_ID('dbo.ProductionOperators')
)
BEGIN
    CREATE INDEX IX_ProductionOperators_ProductionLinesID
    ON dbo.ProductionOperators(ProductionLinesID);
END;
GO
