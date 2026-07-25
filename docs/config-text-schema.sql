-- Reference only. The real schema is owned by the ConfigTextDbContext EF Core migration under
-- src/OrangepuffPortal.ConfigText/Infrastructure/Migrations — this script documents the resulting
-- shape, it is not run directly.

IF SCHEMA_ID('configtext') IS NULL
BEGIN
    EXEC('CREATE SCHEMA configtext');
END
GO

CREATE TABLE configtext.ConfigTextDefinition
(
    iId             INT IDENTITY(1,1)  NOT NULL,
    sModule         VARCHAR(60)        NOT NULL,   -- owning app/module, e.g. 'OCRWeb.ProjectManagement'
    sTextCode       VARCHAR(60)        NOT NULL,
    sCultureCode    VARCHAR(10)        NOT NULL,   -- '*' = fallback row, or a real culture e.g. 'en-US'
    sTextType       VARCHAR(10)        NOT NULL,   -- 'msg', 'lbl'
    sText           NVARCHAR(1000)     NOT NULL,
    iInsertedUserId INT                NULL,
    dtInsertedTime  DATETIME           NULL,
    iUpdatedUserId  INT                NULL,
    dtUpdatedTime   DATETIME           NULL,
    sNote           NCHAR(255)         NULL,

    CONSTRAINT PK_ConfigTextDefinition PRIMARY KEY CLUSTERED (iId)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX UQ_ConfigTextDefinition_Module_Code_Culture_Type
    ON configtext.ConfigTextDefinition (sModule, sTextCode, sCultureCode, sTextType);
GO
