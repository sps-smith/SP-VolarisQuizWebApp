/*
Reporting access database tasks
Run this against the Volaris Quiz database before deploying the scoped reporting code.

This script is intentionally idempotent for table/index creation. It does not
guess portfolio IDs or org hierarchy values; use the validation result sets at
the bottom to confirm source data before go-live.
*/

SET NOCOUNT ON;

PRINT '1. Preflight: required source tables and columns';

IF OBJECT_ID('dbo.Users', 'U') IS NULL
    THROW 51000, 'Required table dbo.Users is missing.', 1;

IF OBJECT_ID('dbo.VBUMasterListProd', 'U') IS NULL
    THROW 51001, 'Required table dbo.VBUMasterListProd is missing.', 1;

IF OBJECT_ID('dbo.Portfolios', 'U') IS NULL
BEGIN
    PRINT 'dbo.Portfolios does not exist. Creating an INT-keyed table; populate it from the approved portfolio source before assigning Portfolio Admins.';

    CREATE TABLE dbo.Portfolios
    (
        PortfolioID   INT              NOT NULL,
        PortfolioName NVARCHAR(200)    NOT NULL,
        CreatedAt     DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_Portfolios_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_Portfolios PRIMARY KEY (PortfolioID),
        CONSTRAINT UQ_Portfolios_Name UNIQUE (PortfolioName)
    );
END;

IF COL_LENGTH('dbo.Users', 'OrganizationID') IS NULL
    THROW 51002, 'Required column dbo.Users.OrganizationID is missing.', 1;

IF COL_LENGTH('dbo.Users', 'PortfolioID') IS NULL
    THROW 51003, 'Required column dbo.Users.PortfolioID is missing.', 1;

IF COL_LENGTH('dbo.VBUMasterListProd', 'OrganizationID') IS NULL
    THROW 51004, 'Required column dbo.VBUMasterListProd.OrganizationID is missing.', 1;

IF COL_LENGTH('dbo.VBUMasterListProd', 'ReportToID') IS NULL
    THROW 51005, 'Required column dbo.VBUMasterListProd.ReportToID is missing.', 1;

IF COL_LENGTH('dbo.Portfolios', 'PortfolioID') IS NULL
    THROW 51007, 'Required column dbo.Portfolios.PortfolioID is missing.', 1;

IF COL_LENGTH('dbo.Portfolios', 'PortfolioName') IS NULL
    THROW 51008, 'Required column dbo.Portfolios.PortfolioName is missing.', 1;

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Users'
      AND COLUMN_NAME IN ('OrganizationID', 'PortfolioID')
      AND DATA_TYPE <> 'int'
)
    THROW 51009, 'dbo.Users.OrganizationID and dbo.Users.PortfolioID must both be INT before deploying reporting scopes.', 1;

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'VBUMasterListProd'
      AND COLUMN_NAME IN ('OrganizationID', 'ReportToID')
      AND DATA_TYPE <> 'int'
)
    THROW 51010, 'dbo.VBUMasterListProd.OrganizationID and ReportToID must be INT.', 1;

IF EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Portfolios'
      AND COLUMN_NAME = 'PortfolioID'
      AND DATA_TYPE <> 'int'
)
    THROW 51011, 'dbo.Portfolios.PortfolioID must be INT.', 1;

PRINT '2. Creating scoped access assignment tables';

IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrgAdminAssignments
    (
        AssignmentID    UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_OrgAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID(),
        UserID          UNIQUEIDENTIFIER NOT NULL,
        OrganizationID  INT              NOT NULL,
        AssignedAt      DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_OrgAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET(),
        AssignedByUserID UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_OrgAdminAssignments PRIMARY KEY (AssignmentID),
        CONSTRAINT UQ_OAA_UserOrg UNIQUE (UserID, OrganizationID),
        CONSTRAINT FK_OAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_OAA_Org FOREIGN KEY (OrganizationID) REFERENCES dbo.Organizations(OrganizationID),
        CONSTRAINT FK_OAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID)
    );
END;
ELSE
BEGIN
    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignmentID') IS NULL
        ALTER TABLE dbo.OrgAdminAssignments ADD AssignmentID UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedAt') IS NULL
        ALTER TABLE dbo.OrgAdminAssignments ADD AssignedAt DATETIMEOFFSET(3) NULL;
    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedByUserID') IS NULL
        ALTER TABLE dbo.OrgAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH('dbo.OrgAdminAssignments', 'CreatedByUserID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
             AND name = 'CreatedByUserID'
             AND is_nullable = 0
       )
        ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;
END;
GO

IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.OrgAdminAssignments
    SET AssignmentID = ISNULL(AssignmentID, NEWID()),
        AssignedAt = ISNULL(AssignedAt, SYSDATETIMEOFFSET()),
        AssignedByUserID = ISNULL(AssignedByUserID, UserID);

    DECLARE @OrgLegacySql NVARCHAR(MAX) = N'
        UPDATE dbo.OrgAdminAssignments
        SET AssignedByUserID = ISNULL(AssignedByUserID, ' + CASE
            WHEN COL_LENGTH('dbo.OrgAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN N'CreatedByUserID'
            ELSE N'UserID'
        END + N');';
    EXEC(@OrgLegacySql);

    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignmentID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
             AND name = 'AssignmentID'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignmentID UNIQUEIDENTIFIER NOT NULL;

    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedAt') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
             AND name = 'AssignedAt'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignedAt DATETIMEOFFSET(3) NOT NULL;

    IF COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedByUserID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments')
             AND name = 'AssignedByUserID'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NOT NULL;
END;

GO

IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
BEGIN
    DECLARE @OaaPk sysname = (
        SELECT kc.name
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
          AND kc.[type] = 'PK'
    );

    IF @OaaPk IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM sys.index_columns ic
           JOIN sys.columns c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
           WHERE ic.object_id = OBJECT_ID('dbo.OrgAdminAssignments')
             AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.OrgAdminAssignments') AND name = @OaaPk)
             AND ic.key_ordinal = 1
             AND c.name = 'AssignmentID'
       )
        EXEC('ALTER TABLE dbo.OrgAdminAssignments DROP CONSTRAINT [' + @OaaPk + ']');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
          AND c.name = 'AssignmentID'
    )
        ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT DF_OrgAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID() FOR AssignmentID;
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.OrgAdminAssignments')
          AND c.name = 'AssignedAt'
    )
        ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT DF_OrgAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET() FOR AssignedAt;

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_OrgAdminAssignments')
        ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT PK_OrgAdminAssignments PRIMARY KEY (AssignmentID);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_OAA_UserOrg' AND object_id = OBJECT_ID('dbo.OrgAdminAssignments'))
        ALTER TABLE dbo.OrgAdminAssignments ADD CONSTRAINT UQ_OAA_UserOrg UNIQUE (UserID, OrganizationID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_User')
        ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_Org')
        ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_Org FOREIGN KEY (OrganizationID) REFERENCES dbo.Organizations(OrganizationID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OAA_AssignedBy')
        ALTER TABLE dbo.OrgAdminAssignments WITH CHECK ADD CONSTRAINT FK_OAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID);
END;

GO

IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortfolioAdminAssignments
    (
        AssignmentID     UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PortfolioAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID(),
        UserID           UNIQUEIDENTIFIER NOT NULL,
        PortfolioID      INT              NOT NULL,
        AssignedAt       DATETIMEOFFSET(3) NOT NULL CONSTRAINT DF_PortfolioAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET(),
        AssignedByUserID UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_PortfolioAdminAssignments PRIMARY KEY (AssignmentID),
        CONSTRAINT UQ_PAA_UserPortfolio UNIQUE (UserID, PortfolioID),
        CONSTRAINT FK_PAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
        CONSTRAINT FK_PAA_Portfolio FOREIGN KEY (PortfolioID) REFERENCES dbo.Portfolios(PortfolioID),
        CONSTRAINT FK_PAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID)
    );
END;
ELSE
BEGIN
    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignmentID') IS NULL
        ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignmentID UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedAt') IS NULL
        ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignedAt DATETIMEOFFSET(3) NULL;
    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedByUserID') IS NULL
        ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'CreatedByUserID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
             AND name = 'CreatedByUserID'
             AND is_nullable = 0
       )
        ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;
END;
GO

IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
BEGIN
    UPDATE dbo.PortfolioAdminAssignments
    SET AssignmentID = ISNULL(AssignmentID, NEWID()),
        AssignedAt = ISNULL(AssignedAt, SYSDATETIMEOFFSET()),
        AssignedByUserID = ISNULL(AssignedByUserID, UserID);

    DECLARE @PortfolioLegacySql NVARCHAR(MAX) = N'
        UPDATE dbo.PortfolioAdminAssignments
        SET AssignedByUserID = ISNULL(AssignedByUserID, ' + CASE
            WHEN COL_LENGTH('dbo.PortfolioAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN N'CreatedByUserID'
            ELSE N'UserID'
        END + N');';
    EXEC(@PortfolioLegacySql);

    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignmentID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
             AND name = 'AssignmentID'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignmentID UNIQUEIDENTIFIER NOT NULL;

    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedAt') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
             AND name = 'AssignedAt'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignedAt DATETIMEOFFSET(3) NOT NULL;

    IF COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedByUserID') IS NOT NULL
       AND EXISTS (
           SELECT 1
           FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
             AND name = 'AssignedByUserID'
             AND is_nullable = 1
       )
        ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NOT NULL;
END;

GO

IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
BEGIN
    DECLARE @PaaPk sysname = (
        SELECT kc.name
        FROM sys.key_constraints kc
        WHERE kc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
          AND kc.[type] = 'PK'
    );

    IF @PaaPk IS NOT NULL
       AND NOT EXISTS (
           SELECT 1
           FROM sys.index_columns ic
           JOIN sys.columns c
                ON c.object_id = ic.object_id
               AND c.column_id = ic.column_id
           WHERE ic.object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
             AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.PortfolioAdminAssignments') AND name = @PaaPk)
             AND ic.key_ordinal = 1
             AND c.name = 'AssignmentID'
       )
        EXEC('ALTER TABLE dbo.PortfolioAdminAssignments DROP CONSTRAINT [' + @PaaPk + ']');

    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
          AND c.name = 'AssignmentID'
    )
        ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT DF_PortfolioAdminAssignments_AssignmentID DEFAULT NEWSEQUENTIALID() FOR AssignmentID;
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        JOIN sys.columns c
          ON c.object_id = dc.parent_object_id
         AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.PortfolioAdminAssignments')
          AND c.name = 'AssignedAt'
    )
        ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT DF_PortfolioAdminAssignments_AssignedAt DEFAULT SYSDATETIMEOFFSET() FOR AssignedAt;

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_PortfolioAdminAssignments')
        ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT PK_PortfolioAdminAssignments PRIMARY KEY (AssignmentID);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_PAA_UserPortfolio' AND object_id = OBJECT_ID('dbo.PortfolioAdminAssignments'))
        ALTER TABLE dbo.PortfolioAdminAssignments ADD CONSTRAINT UQ_PAA_UserPortfolio UNIQUE (UserID, PortfolioID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_User')
        ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_User FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_Portfolio')
        ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_Portfolio FOREIGN KEY (PortfolioID) REFERENCES dbo.Portfolios(PortfolioID);
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PAA_AssignedBy')
        ALTER TABLE dbo.PortfolioAdminAssignments WITH CHECK ADD CONSTRAINT FK_PAA_AssignedBy FOREIGN KEY (AssignedByUserID) REFERENCES dbo.Users(UserID);
END;

GO

PRINT '3. Creating supporting indexes';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrgAdminAssignments_OrganizationID' AND object_id = OBJECT_ID('dbo.OrgAdminAssignments'))
    CREATE INDEX IX_OrgAdminAssignments_OrganizationID ON dbo.OrgAdminAssignments(OrganizationID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PortfolioAdminAssignments_PortfolioID' AND object_id = OBJECT_ID('dbo.PortfolioAdminAssignments'))
    CREATE INDEX IX_PortfolioAdminAssignments_PortfolioID ON dbo.PortfolioAdminAssignments(PortfolioID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VBUMasterListProd_OrganizationID' AND object_id = OBJECT_ID('dbo.VBUMasterListProd'))
    CREATE INDEX IX_VBUMasterListProd_OrganizationID ON dbo.VBUMasterListProd(OrganizationID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VBUMasterListProd_ReportToID' AND object_id = OBJECT_ID('dbo.VBUMasterListProd'))
    CREATE INDEX IX_VBUMasterListProd_ReportToID ON dbo.VBUMasterListProd(ReportToID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_OrganizationID' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_OrganizationID ON dbo.Users(OrganizationID);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Users_PortfolioID' AND object_id = OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_PortfolioID ON dbo.Users(PortfolioID);

PRINT '4. Manual portfolio setup placeholders';

/*
Add any missing portfolio or sub-portfolio rows before assigning Portfolio Admins.
Use approved PortfolioID values from the source-of-truth portfolio data.

Example only:

IF NOT EXISTS (SELECT 1 FROM dbo.Portfolios WHERE PortfolioName = N'Ulsted Germany Sub-Portfolio')
    INSERT dbo.Portfolios (PortfolioID, PortfolioName) VALUES (<approved-int-id>, N'Ulsted Germany Sub-Portfolio');
*/

PRINT '5. Validation result sets';

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND (
        (TABLE_NAME = 'Users' AND COLUMN_NAME IN ('UserID', 'OrganizationID', 'PortfolioID', 'RoleCode'))
     OR (TABLE_NAME = 'VBUMasterListProd' AND COLUMN_NAME IN ('VBUMasterRecordID', 'OrganizationID', 'ReportToID', 'OrganizationName'))
     OR (TABLE_NAME = 'OrgAdminAssignments' AND COLUMN_NAME IN ('AssignmentID', 'UserID', 'OrganizationID', 'AssignedAt', 'AssignedByUserID'))
     OR (TABLE_NAME = 'PortfolioAdminAssignments' AND COLUMN_NAME IN ('AssignmentID', 'UserID', 'PortfolioID', 'AssignedAt', 'AssignedByUserID'))
     OR (TABLE_NAME = 'Portfolios' AND COLUMN_NAME IN ('PortfolioID', 'PortfolioName'))
  )
ORDER BY TABLE_NAME, ORDINAL_POSITION;

SELECT TOP (50)
    v.OrganizationID,
    v.OrganizationName,
    v.ReportToID
FROM dbo.VBUMasterListProd v
WHERE v.OrganizationID = 30000
   OR v.ReportToID = 30000
ORDER BY v.OrganizationID;

SELECT
    u.UserID,
    u.Email,
    u.RoleCode,
    u.OrganizationID,
    u.PortfolioID
FROM dbo.Users u
WHERE u.IsActive = 1
  AND (u.OrganizationID IS NULL OR u.OrganizationID = 0)
ORDER BY u.Email;

SELECT
    p.PortfolioID,
    p.PortfolioName,
    OrgCount = COUNT(DISTINCT u.OrganizationID)
FROM dbo.Portfolios p
LEFT JOIN dbo.Users u ON u.PortfolioID = p.PortfolioID AND u.IsActive = 1 AND u.OrganizationID > 0
GROUP BY p.PortfolioID, p.PortfolioName
ORDER BY p.PortfolioName;

PRINT 'Reporting access database tasks complete.';
