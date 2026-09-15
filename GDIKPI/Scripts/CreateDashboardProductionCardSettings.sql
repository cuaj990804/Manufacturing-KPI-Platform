IF OBJECT_ID(N'[dbo].[DashboardProductionCardSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DashboardProductionCardSettings]
    (
        [DashboardProductionCardSettingId] INT IDENTITY(1,1) NOT NULL,
        [CardKey] VARCHAR(255) NOT NULL,
        [IsVisible] BIT NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_IsVisible] DEFAULT(1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_SortOrder] DEFAULT(0),
        [UpdatedAt] DATETIME2(0) NOT NULL CONSTRAINT [DF_DashboardProductionCardSettings_UpdatedAt] DEFAULT(SYSDATETIME()),
        CONSTRAINT [PK_DashboardProductionCardSettings] PRIMARY KEY CLUSTERED ([DashboardProductionCardSettingId] ASC)
    );

    CREATE UNIQUE INDEX [UX_DashboardProductionCardSettings_CardKey]
        ON [dbo].[DashboardProductionCardSettings] ([CardKey]);
END;
