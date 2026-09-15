-- Run against master. Never replace an existing database.
IF DB_ID(N'ManufacturingKpiDemo') IS NOT NULL
    THROW 50000, 'ManufacturingKpiDemo already exists. Use a new empty demo database.', 1;
GO
CREATE DATABASE [ManufacturingKpiDemo];
GO
