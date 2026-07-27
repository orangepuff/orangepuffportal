-- ConfigData schema — managed by OrangepuffPortal.ConfigData EF Core migrations.
-- This script is reference-only; run migrations via the host application at startup.

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'configdata')
    EXEC('CREATE SCHEMA [configdata]');
GO

CREATE TABLE [configdata].[ConfigData] (
    [iId]               INT             IDENTITY(1,1) NOT FOR REPLICATION NOT NULL,
    [sKey]              VARCHAR(100)    NOT NULL,
    [sValue]            VARCHAR(MAX)    NULL,
    [iInsertedUserId]   INT             NULL,
    [dtInsertedTime]    DATETIME        NULL,
    [iUpdatedUserId]    INT             NULL,
    [dtUpdatedTime]     DATETIME        NULL,
    [bAllowEditByScreen] BIT            NULL,
    [sDescription]      VARCHAR(255)    NULL,
    CONSTRAINT [PK_ConfigData] PRIMARY KEY CLUSTERED ([iId] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF,
              ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON,
              OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY];
GO

CREATE UNIQUE INDEX [UQ_ConfigData_Key]
    ON [configdata].[ConfigData] ([sKey]);
GO

-- EF migrations history table (created automatically by EF on first migration run)
-- CREATE TABLE [configdata].[__EFMigrationsHistory] ( [MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]) );
