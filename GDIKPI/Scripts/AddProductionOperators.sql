CREATE TABLE ProductionOperators (
    OperatorId INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeNumber INT NOT NULL UNIQUE,
    NameOperator VARCHAR(50),
    LastnameOperator VARCHAR(100),
    AreaId INT,
    ProductionLinesID INT,
    Operation VARCHAR(100),
    Goal INT,
    Active BIT DEFAULT 1,

    CONSTRAINT FK_ProductionOperators_Areas
        FOREIGN KEY (AreaId) REFERENCES Areas(AreaID),
    CONSTRAINT FK_ProductionOperators_ProductionLines
        FOREIGN KEY (ProductionLinesID) REFERENCES ProductionLines(ProductionLinesID)
);
CREATE TABLE ProductionOperatorsScans (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    OperatorId INT NOT NULL,
    Code VARCHAR(50),
    ScannedAt DATETIME DEFAULT GETDATE()

)
--Índices recomendados (importante para rendimiento)

-- Buscar rápido por número de empleado
CREATE INDEX IX_ProductionOperators_EmployeeNumber
ON ProductionOperators(EmployeeNumber);

CREATE INDEX IX_ProductionOperators_ProductionLinesID
ON ProductionOperators(ProductionLinesID);

-- Consultas por operador y fecha
CREATE INDEX IX_Production_Operator_Time
ON Production(OperatorId, TimeScane);


-- Employee seed data is intentionally excluded from this public repository.
