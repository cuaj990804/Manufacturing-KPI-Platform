-- Add IsActive column to ProductionLines table
USE [KPIS]
GO

-- Add the IsActive column with default value TRUE
ALTER TABLE [dbo].[ProductionLines]
ADD [IsActive] BIT NOT NULL DEFAULT 1
GO

PRINT 'IsActive column added to ProductionLines table successfully'