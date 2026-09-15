-- Public demo schema. No production records, users, logins, or server paths are exported.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF DB_NAME() IN ('master', 'model', 'msdb', 'tempdb')
    THROW 50001, 'Select an empty demo database before running this script.', 1;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 50002, 'Schema installation requires an empty database.', 1;
GO
CREATE TABLE [dbo].[Permissions](
	[PermissionID] [int] IDENTITY(1,1) NOT NULL,
	[Name] [varchar](255) NOT NULL,
	[Description] [varchar](255) NULL,
PRIMARY KEY CLUSTERED
(
	[PermissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON),
UNIQUE NONCLUSTERED
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[UserLinePermissions](
	[UserLinePermissionID] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[PermissionID] [int] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[UserLinePermissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[ProductionLines](
	[ProductionLinesID] [int] IDENTITY(1,1) NOT NULL,
	[LineNumber] [int] NULL,
	[DailyGoal] [int] NOT NULL,
	[PersonalQuantity] [int] NOT NULL,
	[ShiftNumber] [int] NOT NULL,
	[AreaID] [int] NULL,
	[IsActive] [bit] NOT NULL,
	[LineName] [nvarchar](100) NULL,
	[StandardTime] [decimal](18, 8) NULL,
PRIMARY KEY CLUSTERED
(
	[ProductionLinesID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Areas](
	[AreaID] [int] IDENTITY(1,1) NOT NULL,
	[AreaName] [nvarchar](255) NOT NULL,
	[CustomerName] [nvarchar](255) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AreaID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Users](
	[EmployeeNumber] [varchar](50) NOT NULL,
	[Name] [varchar](255) NULL,
	[Role] [varchar](255) NULL,
	[Area] [nvarchar](50) NULL,
 CONSTRAINT [PK_Users] PRIMARY KEY CLUSTERED
(
	[EmployeeNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Breaks](
	[BreakID] [int] IDENTITY(1,1) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[BreakStart] [time](7) NULL,
	[BreakEnd] [time](7) NULL,
PRIMARY KEY CLUSTERED
(
	[BreakID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[DowntimeEvents](
	[DowntimeID] [int] IDENTITY(1,1) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NULL,
	[Reason] [varchar](255) NULL,
	[Status] [varchar](255) NULL,
	[ClosedBy] [varchar](100) NULL,
	[OpenedBy] [varchar](100) NULL,
	[DowntimeCategory] [nvarchar](100) NULL,
PRIMARY KEY CLUSTERED
(
	[DowntimeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Absenteeism](
	[AbsenteeismID] [int] IDENTITY(1,1) NOT NULL,
	[AbsenteeismCategory] [nvarchar](255) NOT NULL,
	[AbsenteeismQuantity] [int] NOT NULL,
	[AbsenteeismDate] [date] NOT NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[AbsenteeismID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[DefectsData](
	[DefectsDataID] [int] IDENTITY(1,1) NOT NULL,
	[RejectionID] [int] NULL,
	[ProductionLinesID] [int] NOT NULL,
	[Category] [varchar](50) NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[DefectID] [int] NOT NULL,
	[DefectQuantity] [int] NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NOT NULL,
	[ScannerValue] [nvarchar](50) NULL,
PRIMARY KEY CLUSTERED
(
	[DefectsDataID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Defects](
	[DefectID] [int] IDENTITY(1,1) NOT NULL,
	[DefectName] [nvarchar](255) NOT NULL,
	[AreaID] [int] NOT NULL,
	[DefectCategoryID] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[DefectID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Rejections](
	[RejectionID] [int] IDENTITY(1,1) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[Category] [varchar](100) NOT NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[DefectQuantity] [int] NULL,
	[StartTime] [datetime] NOT NULL,
	[EndTime] [datetime] NOT NULL,
	[ProgramId] [int] NOT NULL,
	[ProgramDescription] [nvarchar](255) NULL,
PRIMARY KEY CLUSTERED
(
	[RejectionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[DefectsCategory](
	[DefectCategoryID] [int] IDENTITY(1,1) NOT NULL,
	[DefectCategoryName] [nvarchar](255) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[DefectCategoryID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Shifts](
	[ShiftNumber] [int] NOT NULL,
	[StartTime] [time](7) NULL,
	[EndTime] [time](7) NULL,
PRIMARY KEY CLUSTERED
(
	[ShiftNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[ScannerProduction](
	[ScannerProductionId] [bigint] IDENTITY(1,1) NOT NULL,
	[ScannerProductionDateTime] [datetime2](0) NOT NULL,
	[LineId] [int] NOT NULL,
	[PartNumber] [nvarchar](max) NULL,
	[ScannerValue] [nvarchar](500) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[ScannerProductionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[AuditLog](
	[AuditLogID] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeNumber] [varchar](50) NULL,
	[ActionType] [varchar](50) NULL,
	[EntityName] [varchar](200) NULL,
	[EntityID] [int] NULL,
	[Timestamp] [datetime] NULL,
	[Details] [nvarchar](max) NULL,
PRIMARY KEY CLUSTERED
(
	[AuditLogID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[EOOPercentage](
	[EOOPercentageID] [int] IDENTITY(1,1) NOT NULL,
	[MetricName] [varchar](20) NOT NULL,
	[Category] [varchar](20) NOT NULL,
	[MinValue] [decimal](5, 2) NOT NULL,
	[MaxValue] [decimal](5, 2) NOT NULL,
PRIMARY KEY CLUSTERED
(
	[EOOPercentageID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[DashboardProductionCardSettings](
	[DashboardProductionCardSettingId] [int] IDENTITY(1,1) NOT NULL,
	[CardKey] [varchar](255) NOT NULL,
	[IsVisible] [bit] NOT NULL,
	[SortOrder] [int] NOT NULL,
	[UpdatedAt] [datetime2](0) NOT NULL,
 CONSTRAINT [PK_DashboardProductionCardSettings] PRIMARY KEY CLUSTERED
(
	[DashboardProductionCardSettingId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Commands](
	[CommandId] [int] IDENTITY(1,1) NOT NULL,
	[CommandText] [nvarchar](50) NOT NULL,
	[ActionKey] [nvarchar](50) NOT NULL,
	[Scope] [nvarchar](50) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[UpdatedAt] [datetime2](7) NULL,
 CONSTRAINT [PK_Commands] PRIMARY KEY CLUSTERED
(
	[CommandId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON),
 CONSTRAINT [UQ_Commands_Scope_CommandText] UNIQUE NONCLUSTERED
(
	[Scope] ASC,
	[CommandText] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[PerformanceMetrics](
	[MetricID] [int] IDENTITY(1,1) NOT NULL,
	[MetricType] [varchar](50) NOT NULL,
	[MetricName] [varchar](100) NOT NULL,
	[Category] [varchar](100) NULL,
	[MinValue] [decimal](10, 2) NULL,
	[MaxValue] [decimal](10, 2) NULL,
	[Unit] [varchar](20) NULL,
PRIMARY KEY CLUSTERED
(
	[MetricID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[OEEHistory](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RegistrationDate] [datetime] NULL,
	[DateData] [date] NULL,
	[TimeData] [time](7) NULL,
	[ProductionLinesId] [int] NULL,
	[LineNumber] [varchar](50) NULL,
	[AreaCustomerName] [varchar](200) NULL,
	[RequirementGoalPieces] [int] NULL,
	[ProducedPieces] [int] NULL,
	[DowntimeMinutes] [int] NULL,
	[RejectedPieces] [int] NULL,
	[AvailabilityPercentage] [decimal](10, 4) NULL,
	[PerformancePercentage] [decimal](10, 4) NULL,
	[QualityPercentage] [decimal](10, 4) NULL,
	[OeePercentage] [decimal](10, 4) NULL,
	[PlannedMinutes] [int] NULL,
	[OperatingMinutes] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Modules](
	[ModuleID] [int] IDENTITY(1,1) NOT NULL,
	[ModuleName] [varchar](255) NOT NULL,
	[Description] [varchar](255) NULL,
	[IsActive] [bit] NOT NULL,
	[Route] [varchar](255) NULL,
 CONSTRAINT [PK__Module__2B7477E7] PRIMARY KEY CLUSTERED
(
	[ModuleID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[UserModulePermissions](
	[UserModulePermissionID] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[ModuleID] [int] NOT NULL,
	[PermissionID] [int] NOT NULL,
 CONSTRAINT [PK__UserModule__3F6E3CEA] PRIMARY KEY CLUSTERED
(
	[UserModulePermissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[UserAreaPermissions](
	[UserAreaPermissionID] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeNumber] [varchar](50) NOT NULL,
	[AreaID] [int] NOT NULL,
	[PermissionID] [int] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[UserAreaPermissionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[ProductionOperators](
	[OperatorId] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeNumber] [int] NOT NULL,
	[NameOperator] [varchar](50) NULL,
	[LastnameOperator] [varchar](100) NULL,
	[AreaId] [int] NULL,
	[Operation] [varchar](100) NULL,
	[Goal] [int] NULL,
	[Active] [bit] NULL,
	[ProductionLinesID] [int] NULL,
PRIMARY KEY CLUSTERED
(
	[OperatorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON),
UNIQUE NONCLUSTERED
(
	[EmployeeNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[Efficiency](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[RegistrationDate] [datetime] NOT NULL,
	[DateData] [date] NOT NULL,
	[TimeData] [time](0) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[LineNumber] [varchar](50) NOT NULL,
	[AreaCustomerName] [varchar](200) NULL,
	[EfficiencyPercentage] [decimal](10, 4) NOT NULL,
	[PeopleQuantity] [int] NOT NULL,
	[TiempoEstandar] [decimal](12, 8) NOT NULL,
	[HorasUtilizadas] [decimal](10, 4) NULL,
	[HorasGanadas] [decimal](10, 4) NULL,
	[ProducedPieces] [int] NOT NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[ProductionData](
	[ProductionID] [int] IDENTITY(1,1) NOT NULL,
	[ProductionLinesID] [int] NOT NULL,
	[ProductionDate] [date] NOT NULL,
	[StartHour] [time](7) NOT NULL,
	[EndHour] [time](7) NOT NULL,
	[ProducedPieces] [int] NULL,
	[RejectedPieces] [int] NULL,
	[ProgramId] [int] NOT NULL,
	[ProgramDescription] [varchar](255) NULL,
PRIMARY KEY CLUSTERED
(
	[ProductionID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
CREATE TABLE [dbo].[ProductionOperatorsScans](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[OperatorId] [int] NOT NULL,
	[Code] [varchar](50) NULL,
	[ScannedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
)
GO
SET ANSI_PADDING ON
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_DashboardProductionCardSettings_CardKey] ON [dbo].[DashboardProductionCardSettings]
(
	[CardKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_ProductionOperators_Area_Operation_OperatorId] ON [dbo].[ProductionOperators]
(
	[AreaId] ASC,
	[Operation] ASC,
	[OperatorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
CREATE NONCLUSTERED INDEX [IX_ProductionOperators_EmployeeNumber] ON [dbo].[ProductionOperators]
(
	[EmployeeNumber] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
CREATE NONCLUSTERED INDEX [IX_ProductionOperators_ProductionLinesID] ON [dbo].[ProductionOperators]
(
	[ProductionLinesID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
CREATE NONCLUSTERED INDEX [IX_Efficiency_DateData] ON [dbo].[Efficiency]
(
	[DateData] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
CREATE NONCLUSTERED INDEX [IX_Efficiency_ProductionLinesID] ON [dbo].[Efficiency]
(
	[ProductionLinesID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Efficiency_Date_Line] ON [dbo].[Efficiency]
(
	[DateData] ASC,
	[ProductionLinesID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_ProductionOperatorsScans_Code_OperatorId_ScannedAt] ON [dbo].[ProductionOperatorsScans]
(
	[Code] ASC,
	[OperatorId] ASC,
	[ScannedAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
SET ANSI_PADDING ON
GO
CREATE NONCLUSTERED INDEX [IX_ProductionOperatorsScans_ScannedAt] ON [dbo].[ProductionOperatorsScans]
(
	[ScannedAt] DESC
)
INCLUDE ( 	[OperatorId],
	[Code]) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON)
GO
ALTER TABLE [dbo].[ScannerProduction] ADD  DEFAULT (getdate()) FOR [ScannerProductionDateTime]
GO
ALTER TABLE [dbo].[DashboardProductionCardSettings] ADD  CONSTRAINT [DF_DashboardProductionCardSettings_IsVisible]  DEFAULT ((1)) FOR [IsVisible]
GO
ALTER TABLE [dbo].[DashboardProductionCardSettings] ADD  CONSTRAINT [DF_DashboardProductionCardSettings_SortOrder]  DEFAULT ((0)) FOR [SortOrder]
GO
ALTER TABLE [dbo].[DashboardProductionCardSettings] ADD  CONSTRAINT [DF_DashboardProductionCardSettings_UpdatedAt]  DEFAULT (sysdatetime()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Commands] ADD  CONSTRAINT [DF_Commands_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Commands] ADD  CONSTRAINT [DF_Commands_CreatedAt]  DEFAULT (sysdatetime()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[OEEHistory] ADD  DEFAULT (getdate()) FOR [RegistrationDate]
GO
ALTER TABLE [dbo].[Modules] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ProductionLines] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[ProductionOperators] ADD  DEFAULT ((1)) FOR [Active]
GO
ALTER TABLE [dbo].[Rejections] ADD  DEFAULT ((0)) FOR [DefectQuantity]
GO
ALTER TABLE [dbo].[Rejections] ADD  DEFAULT ((0)) FOR [ProgramId]
GO
ALTER TABLE [dbo].[Rejections] ADD  CONSTRAINT [DF_Rejections_ProgramDescription]  DEFAULT ('Sin descripcion') FOR [ProgramDescription]
GO
ALTER TABLE [dbo].[Efficiency] ADD  CONSTRAINT [DF_Efficiency_RegistrationDate]  DEFAULT (getdate()) FOR [RegistrationDate]
GO
ALTER TABLE [dbo].[Efficiency] ADD  DEFAULT ((0)) FOR [PeopleQuantity]
GO
ALTER TABLE [dbo].[Efficiency] ADD  DEFAULT ((0)) FOR [TiempoEstandar]
GO
ALTER TABLE [dbo].[Efficiency] ADD  DEFAULT ((0)) FOR [HorasUtilizadas]
GO
ALTER TABLE [dbo].[Efficiency] ADD  DEFAULT ((0)) FOR [HorasGanadas]
GO
ALTER TABLE [dbo].[Efficiency] ADD  DEFAULT ((0)) FOR [ProducedPieces]
GO
ALTER TABLE [dbo].[ProductionData] ADD  DEFAULT ((0)) FOR [ProducedPieces]
GO
ALTER TABLE [dbo].[ProductionData] ADD  DEFAULT ((0)) FOR [RejectedPieces]
GO
ALTER TABLE [dbo].[ProductionData] ADD  CONSTRAINT [DF_ProductionData_ProgramId]  DEFAULT ((0)) FOR [ProgramId]
GO
ALTER TABLE [dbo].[DefectsData] ADD  DEFAULT ((0)) FOR [DefectQuantity]
GO
ALTER TABLE [dbo].[ProductionOperatorsScans] ADD  DEFAULT (getdate()) FOR [ScannedAt]
GO
ALTER TABLE [dbo].[ProductionLines]  WITH CHECK ADD  CONSTRAINT [Areas_ProductionLines] FOREIGN KEY([AreaID])
REFERENCES [dbo].[Areas] ([AreaID])
GO
ALTER TABLE [dbo].[ProductionLines] CHECK CONSTRAINT [Areas_ProductionLines]
GO
ALTER TABLE [dbo].[ProductionLines]  WITH CHECK ADD  CONSTRAINT [Shifts_ProductionLines] FOREIGN KEY([ShiftNumber])
REFERENCES [dbo].[Shifts] ([ShiftNumber])
GO
ALTER TABLE [dbo].[ProductionLines] CHECK CONSTRAINT [Shifts_ProductionLines]
GO
ALTER TABLE [dbo].[Defects]  WITH CHECK ADD  CONSTRAINT [Areas_Defects] FOREIGN KEY([AreaID])
REFERENCES [dbo].[Areas] ([AreaID])
GO
ALTER TABLE [dbo].[Defects] CHECK CONSTRAINT [Areas_Defects]
GO
ALTER TABLE [dbo].[Defects]  WITH CHECK ADD  CONSTRAINT [FK_Defects_DefectCategory] FOREIGN KEY([DefectCategoryID])
REFERENCES [dbo].[DefectsCategory] ([DefectCategoryID])
GO
ALTER TABLE [dbo].[Defects] CHECK CONSTRAINT [FK_Defects_DefectCategory]
GO
ALTER TABLE [dbo].[UserModulePermissions]  WITH CHECK ADD  CONSTRAINT [UserModulePermissions_Modules] FOREIGN KEY([ModuleID])
REFERENCES [dbo].[Modules] ([ModuleID])
GO
ALTER TABLE [dbo].[UserModulePermissions] CHECK CONSTRAINT [UserModulePermissions_Modules]
GO
ALTER TABLE [dbo].[UserModulePermissions]  WITH CHECK ADD  CONSTRAINT [UserModulePermissions_Permissions] FOREIGN KEY([PermissionID])
REFERENCES [dbo].[Permissions] ([PermissionID])
GO
ALTER TABLE [dbo].[UserModulePermissions] CHECK CONSTRAINT [UserModulePermissions_Permissions]
GO
ALTER TABLE [dbo].[UserModulePermissions]  WITH CHECK ADD  CONSTRAINT [UserModulePermissions_Users] FOREIGN KEY([EmployeeNumber])
REFERENCES [dbo].[Users] ([EmployeeNumber])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserModulePermissions] CHECK CONSTRAINT [UserModulePermissions_Users]
GO
ALTER TABLE [dbo].[UserAreaPermissions]  WITH CHECK ADD  CONSTRAINT [UserAreaPermissions_Areas] FOREIGN KEY([AreaID])
REFERENCES [dbo].[Areas] ([AreaID])
GO
ALTER TABLE [dbo].[UserAreaPermissions] CHECK CONSTRAINT [UserAreaPermissions_Areas]
GO
ALTER TABLE [dbo].[UserAreaPermissions]  WITH CHECK ADD  CONSTRAINT [UserAreaPermissions_Permissions] FOREIGN KEY([PermissionID])
REFERENCES [dbo].[Permissions] ([PermissionID])
GO
ALTER TABLE [dbo].[UserAreaPermissions] CHECK CONSTRAINT [UserAreaPermissions_Permissions]
GO
ALTER TABLE [dbo].[UserAreaPermissions]  WITH CHECK ADD  CONSTRAINT [UserAreaPermissions_Users] FOREIGN KEY([EmployeeNumber])
REFERENCES [dbo].[Users] ([EmployeeNumber])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserAreaPermissions] CHECK CONSTRAINT [UserAreaPermissions_Users]
GO
ALTER TABLE [dbo].[ProductionOperators]  WITH CHECK ADD  CONSTRAINT [FK_ProductionOperators_Areas] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([AreaID])
GO
ALTER TABLE [dbo].[ProductionOperators] CHECK CONSTRAINT [FK_ProductionOperators_Areas]
GO
ALTER TABLE [dbo].[ProductionOperators]  WITH CHECK ADD  CONSTRAINT [FK_ProductionOperators_ProductionLines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[ProductionOperators] CHECK CONSTRAINT [FK_ProductionOperators_ProductionLines]
GO
ALTER TABLE [dbo].[UserLinePermissions]  WITH CHECK ADD  CONSTRAINT [UserLinePermissions_Lines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[UserLinePermissions] CHECK CONSTRAINT [UserLinePermissions_Lines]
GO
ALTER TABLE [dbo].[UserLinePermissions]  WITH CHECK ADD  CONSTRAINT [UserLinePermissions_Permissions] FOREIGN KEY([PermissionID])
REFERENCES [dbo].[Permissions] ([PermissionID])
GO
ALTER TABLE [dbo].[UserLinePermissions] CHECK CONSTRAINT [UserLinePermissions_Permissions]
GO
ALTER TABLE [dbo].[UserLinePermissions]  WITH CHECK ADD  CONSTRAINT [UserLinePermissions_Users] FOREIGN KEY([EmployeeNumber])
REFERENCES [dbo].[Users] ([EmployeeNumber])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UserLinePermissions] CHECK CONSTRAINT [UserLinePermissions_Users]
GO
ALTER TABLE [dbo].[Rejections]  WITH CHECK ADD  CONSTRAINT [ProductionLines_Rejections] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[Rejections] CHECK CONSTRAINT [ProductionLines_Rejections]
GO
ALTER TABLE [dbo].[Absenteeism]  WITH CHECK ADD  CONSTRAINT [Absenteeism_ProductionLines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[Absenteeism] CHECK CONSTRAINT [Absenteeism_ProductionLines]
GO
ALTER TABLE [dbo].[Absenteeism]  WITH CHECK ADD  CONSTRAINT [Absenteeism_Users] FOREIGN KEY([EmployeeNumber])
REFERENCES [dbo].[Users] ([EmployeeNumber])
GO
ALTER TABLE [dbo].[Absenteeism] CHECK CONSTRAINT [Absenteeism_Users]
GO
ALTER TABLE [dbo].[Breaks]  WITH CHECK ADD  CONSTRAINT [Breaks_ProductionLines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[Breaks] CHECK CONSTRAINT [Breaks_ProductionLines]
GO
ALTER TABLE [dbo].[Efficiency]  WITH CHECK ADD  CONSTRAINT [FK_Efficiency_ProductionLines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[Efficiency] CHECK CONSTRAINT [FK_Efficiency_ProductionLines]
GO
ALTER TABLE [dbo].[DowntimeEvents]  WITH CHECK ADD  CONSTRAINT [DowntimeEvents_ProductionLines] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[DowntimeEvents] CHECK CONSTRAINT [DowntimeEvents_ProductionLines]
GO
ALTER TABLE [dbo].[ProductionData]  WITH CHECK ADD  CONSTRAINT [ProductionLines_ProductionData] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[ProductionData] CHECK CONSTRAINT [ProductionLines_ProductionData]
GO
ALTER TABLE [dbo].[DefectsData]  WITH CHECK ADD  CONSTRAINT [DefectsData_Defects] FOREIGN KEY([DefectID])
REFERENCES [dbo].[Defects] ([DefectID])
GO
ALTER TABLE [dbo].[DefectsData] CHECK CONSTRAINT [DefectsData_Defects]
GO
ALTER TABLE [dbo].[DefectsData]  WITH CHECK ADD  CONSTRAINT [ProductionData_DefectData] FOREIGN KEY([ProductionLinesID])
REFERENCES [dbo].[ProductionLines] ([ProductionLinesID])
GO
ALTER TABLE [dbo].[DefectsData] CHECK CONSTRAINT [ProductionData_DefectData]
GO
ALTER TABLE [dbo].[DefectsData]  WITH CHECK ADD  CONSTRAINT [Rejection_DefectData] FOREIGN KEY([RejectionID])
REFERENCES [dbo].[Rejections] ([RejectionID])
GO
ALTER TABLE [dbo].[DefectsData] CHECK CONSTRAINT [Rejection_DefectData]
GO
ALTER TABLE [dbo].[ProductionOperatorsScans]  WITH CHECK ADD  CONSTRAINT [FK_ProductionScans_ProductionOperators] FOREIGN KEY([OperatorId])
REFERENCES [dbo].[ProductionOperators] ([OperatorId])
GO
ALTER TABLE [dbo].[ProductionOperatorsScans] CHECK CONSTRAINT [FK_ProductionScans_ProductionOperators]
GO
ALTER TABLE [dbo].[Efficiency]  WITH CHECK ADD  CONSTRAINT [CK_Efficiency_EfficiencyPercentage] CHECK  (([EfficiencyPercentage]>=(0)))
GO
ALTER TABLE [dbo].[Efficiency] CHECK CONSTRAINT [CK_Efficiency_EfficiencyPercentage]
GO
