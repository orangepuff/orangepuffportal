-- Reference only. The real schema is owned by the ConfigDbContext EF Core migration under
-- src/OrangepuffPortal.Config/Infrastructure/Migrations — this script documents the resulting
-- shape, it is not run directly.

IF SCHEMA_ID('config') IS NULL
BEGIN
    EXEC('CREATE SCHEMA config');
END
GO

CREATE TABLE config.ConfigSections
(
    iId             INT IDENTITY(1,1)  NOT NULL,
    sModule         VARCHAR(60)        NOT NULL,
    sSectionDesc    NVARCHAR(255)      NOT NULL,
    sTextCode       VARCHAR(100)       NOT NULL,
    btShow          BIT                NOT NULL CONSTRAINT DF_ConfigSections_btShow DEFAULT (1),
    iSortOrder      INT                NULL,
    iInsertedUserId INT                NULL,
    dtInsertedTime  DATETIME           NULL,
    iUpdatedUserId  INT                NULL,
    dtUpdatedTime   DATETIME           NULL,

    CONSTRAINT PK_ConfigSections PRIMARY KEY CLUSTERED (iId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_ConfigSections_Module_TextCode
    ON config.ConfigSections (sModule, sTextCode);
GO

CREATE TABLE config.Configs
(
    iId             INT IDENTITY(1,1)  NOT NULL,
    iSectionId      INT                NOT NULL,
    sConfigCode     VARCHAR(60)        NOT NULL,
    sConfigName     NVARCHAR(255)      NOT NULL,
    sTextCode       VARCHAR(100)       NOT NULL,
    iConfigType     INT                NOT NULL CONSTRAINT DF_Configs_iConfigType DEFAULT (0),
    btShow          BIT                NOT NULL CONSTRAINT DF_Configs_btShow DEFAULT (1),
    btAllowUserEdit BIT                NOT NULL CONSTRAINT DF_Configs_btAllowUserEdit DEFAULT (0),
    iSortOrder      INT                NULL,
    iInsertedUserId INT                NULL,
    dtInsertedTime  DATETIME           NULL,
    iUpdatedUserId  INT                NULL,
    dtUpdatedTime   DATETIME           NULL,

    CONSTRAINT PK_Configs PRIMARY KEY CLUSTERED (iId),
    CONSTRAINT FK_Configs_ConfigSections FOREIGN KEY (iSectionId) REFERENCES config.ConfigSections (iId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_Configs_ConfigCode
    ON config.Configs (sConfigCode);
GO

CREATE TABLE config.ConfigUsers
(
    iId             INT IDENTITY(1,1)  NOT NULL,
    iUserId         INT                NOT NULL,
    iConfigId       INT                NOT NULL,
    sConfigValue    NVARCHAR(255)      NULL,
    iConfigValue    INT                NULL,
    nConfigValue    DECIMAL(8,3)       NULL,
    btConfigValue   BIT                NULL,
    btActive        BIT                NOT NULL CONSTRAINT DF_ConfigUsers_btActive DEFAULT (1),
    iInsertedUserId INT                NULL,
    dtInsertedTime  DATETIME           NULL,
    iUpdatedUserId  INT                NULL,
    dtUpdatedTime   DATETIME           NULL,

    CONSTRAINT PK_ConfigUsers PRIMARY KEY CLUSTERED (iId),
    CONSTRAINT FK_ConfigUsers_Configs FOREIGN KEY (iConfigId) REFERENCES config.Configs (iId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_ConfigUsers_User_Config
    ON config.ConfigUsers (iUserId, iConfigId);
GO

CREATE TABLE config.ConfigUsersHistory
(
    iId             INT IDENTITY(1,1)  NOT NULL,
    iConfigUserId   INT                NOT NULL,
    iUserId         INT                NOT NULL,
    iConfigId       INT                NOT NULL,
    sConfigValue    NVARCHAR(255)      NULL,
    iConfigValue    INT                NULL,
    nConfigValue    DECIMAL(8,3)       NULL,
    btConfigValue   BIT                NULL,
    btActive        BIT                NOT NULL CONSTRAINT DF_ConfigUsersHistory_btActive DEFAULT (1),
    iInsertedUserId INT                NULL,
    dtInsertedTime  DATETIME           NULL,
    iUpdatedUserId  INT                NULL, -- vestigial: append-only, never set
    dtUpdatedTime   DATETIME           NULL, -- vestigial: append-only, never set

    CONSTRAINT PK_ConfigUsersHistory PRIMARY KEY CLUSTERED (iId),
    CONSTRAINT FK_ConfigUsersHistory_ConfigUsers FOREIGN KEY (iConfigUserId) REFERENCES config.ConfigUsers (iId)
);
GO

CREATE NONCLUSTERED INDEX IX_ConfigUsersHistory_ConfigUserId
    ON config.ConfigUsersHistory (iConfigUserId);
GO
