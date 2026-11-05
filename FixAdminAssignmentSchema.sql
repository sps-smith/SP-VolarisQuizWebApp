-- FixAdminAssignmentSchema.sql
-- Repairs legacy admin assignment tables that still have CreatedByUserID.
-- Run this against the production VolarisQuizDB before retrying the role change.

SET NOCOUNT ON;

PRINT '1. Inspecting current admin assignment columns';

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    IS_NULLABLE,
    DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('OrgAdminAssignments', 'PortfolioAdminAssignments')
  AND COLUMN_NAME IN ('AssignmentID', 'UserID', 'OrganizationID', 'PortfolioID', 'AssignedAt', 'AssignedByUserID', 'CreatedByUserID')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

PRINT '2. Normalizing OrgAdminAssignments';

IF OBJECT_ID('dbo.OrgAdminAssignments', 'U') IS NOT NULL
BEGIN
    DECLARE @hasOrgAssigned bit = CASE WHEN COL_LENGTH('dbo.OrgAdminAssignments', 'AssignedByUserID') IS NOT NULL THEN 1 ELSE 0 END;
    DECLARE @hasOrgCreated bit = CASE WHEN COL_LENGTH('dbo.OrgAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN 1 ELSE 0 END;
    DECLARE @orgSql nvarchar(max);

    IF @hasOrgAssigned = 0
        EXEC(N'ALTER TABLE dbo.OrgAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;');

    SET @orgSql = N'UPDATE dbo.OrgAdminAssignments SET AssignedByUserID = COALESCE(AssignedByUserID, ';
    IF @hasOrgCreated = 1
        SET @orgSql += N'CreatedByUserID, ';
    SET @orgSql += N'UserID);';
    EXEC(@orgSql);

    IF @hasOrgCreated = 1
        EXEC(N'ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;');

    IF @hasOrgAssigned = 1
        EXEC(N'ALTER TABLE dbo.OrgAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NULL;');
END;

PRINT '3. Normalizing PortfolioAdminAssignments';

IF OBJECT_ID('dbo.PortfolioAdminAssignments', 'U') IS NOT NULL
BEGIN
    DECLARE @hasPortfolioAssigned bit = CASE WHEN COL_LENGTH('dbo.PortfolioAdminAssignments', 'AssignedByUserID') IS NOT NULL THEN 1 ELSE 0 END;
    DECLARE @hasPortfolioCreated bit = CASE WHEN COL_LENGTH('dbo.PortfolioAdminAssignments', 'CreatedByUserID') IS NOT NULL THEN 1 ELSE 0 END;
    DECLARE @portfolioSql nvarchar(max);

    IF @hasPortfolioAssigned = 0
        EXEC(N'ALTER TABLE dbo.PortfolioAdminAssignments ADD AssignedByUserID UNIQUEIDENTIFIER NULL;');

    SET @portfolioSql = N'UPDATE dbo.PortfolioAdminAssignments SET AssignedByUserID = COALESCE(AssignedByUserID, ';
    IF @hasPortfolioCreated = 1
        SET @portfolioSql += N'CreatedByUserID, ';
    SET @portfolioSql += N'UserID);';
    EXEC(@portfolioSql);

    IF @hasPortfolioCreated = 1
        EXEC(N'ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN CreatedByUserID UNIQUEIDENTIFIER NULL;');

    IF @hasPortfolioAssigned = 1
        EXEC(N'ALTER TABLE dbo.PortfolioAdminAssignments ALTER COLUMN AssignedByUserID UNIQUEIDENTIFIER NULL;');
END;

PRINT '4. Verifying post-fix schema';

SELECT
    TABLE_NAME,
    COLUMN_NAME,
    IS_NULLABLE,
    DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME IN ('OrgAdminAssignments', 'PortfolioAdminAssignments')
  AND COLUMN_NAME IN ('AssignedByUserID', 'CreatedByUserID')
ORDER BY TABLE_NAME, ORDINAL_POSITION;

PRINT 'Done.';
