SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*
    Final script: Language completion dashboard cache
    -------------------------------------------------
    Purpose:
      - Persist learner completion state by user/language in SQL Server
      - Persist the aggregated language summary used by the GlobalAdmin dashboard
      - Keep the cache current as quiz attempts, users, and published modules change

    Business rules implemented:
      - A user is Completed when they have passed every active + published module
      - Passing means at least one QuizAttempts row with Status = 'Passed'
      - Grandfather rule: if a user had passed at least 9 modules on or before 2025-12-31,
        they are also considered Completed
      - Any active user who matches neither rule is InProgress

    Notes:
      - This script caches all active users (Users.IsActive = 1)
      - "Leavers" is not included because a SQL-side business rule has not been provided
      - Totals are returned by the read procedure rather than stored as a separate row
*/

IF NOT EXISTS (
    SELECT 1
    FROM sys.table_types tt
    JOIN sys.schemas s
      ON s.schema_id = tt.schema_id
    WHERE tt.name = 'GuidList'
      AND s.name = 'dbo'
)
EXEC('CREATE TYPE dbo.GuidList AS TABLE (Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);');
GO

IF OBJECT_ID('dbo.ReportingUserLanguageCompletionCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportingUserLanguageCompletionCache
    (
        UserID                         UNIQUEIDENTIFIER NOT NULL,
        PreferredLanguage              CHAR(2)          NOT NULL,
        LanguageName                   NVARCHAR(100)    NOT NULL,
        PublishedModuleCount           INT              NOT NULL,
        PassedPublishedModuleCount     INT              NOT NULL,
        GrandfatheredPassedModuleCount INT              NOT NULL
            CONSTRAINT DF_ReportingUserLanguageCompletionCache_GrandfatheredPassedModuleCount DEFAULT 0,
        IsGrandfathered                BIT              NOT NULL
            CONSTRAINT DF_ReportingUserLanguageCompletionCache_IsGrandfathered DEFAULT 0,
        IsCompleted                    BIT              NOT NULL,
        IsInProgress                   BIT              NOT NULL,
        LastAttemptAt                  DATETIMEOFFSET(3) NULL,
        LastCalculatedAt               DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT DF_ReportingUserLanguageCompletionCache_LastCalculatedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_ReportingUserLanguageCompletionCache PRIMARY KEY (UserID)
    );
END;
GO

IF COL_LENGTH('dbo.ReportingUserLanguageCompletionCache', 'GrandfatheredPassedModuleCount') IS NULL
BEGIN
    ALTER TABLE dbo.ReportingUserLanguageCompletionCache
        ADD GrandfatheredPassedModuleCount INT NOT NULL
            CONSTRAINT DF_ReportingUserLanguageCompletionCache_GrandfatheredPassedModuleCount_Alter DEFAULT 0;
END;
GO

IF COL_LENGTH('dbo.ReportingUserLanguageCompletionCache', 'IsGrandfathered') IS NULL
BEGIN
    ALTER TABLE dbo.ReportingUserLanguageCompletionCache
        ADD IsGrandfathered BIT NOT NULL
            CONSTRAINT DF_ReportingUserLanguageCompletionCache_IsGrandfathered_Alter DEFAULT 0;
END;
GO

IF OBJECT_ID('dbo.ReportingLanguageSummaryCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportingLanguageSummaryCache
    (
        LanguageCode    CHAR(2)           NOT NULL,
        LanguageName    NVARCHAR(100)     NOT NULL,
        InProgressCount INT               NOT NULL,
        CompletedCount  INT               NOT NULL,
        TotalUsers      INT               NOT NULL,
        LastRefreshedAt DATETIMEOFFSET(3) NOT NULL
            CONSTRAINT DF_ReportingLanguageSummaryCache_LastRefreshedAt DEFAULT SYSDATETIMEOFFSET(),
        CONSTRAINT PK_ReportingLanguageSummaryCache PRIMARY KEY (LanguageCode)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Users_IsActive_RoleCode_PreferredLanguage'
      AND object_id = OBJECT_ID('dbo.Users')
)
BEGIN
    CREATE INDEX IX_Users_IsActive_RoleCode_PreferredLanguage
        ON dbo.Users(IsActive, RoleCode, PreferredLanguage)
        INCLUDE (UserID);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_Quizzes_IsActive_IsPublished'
      AND object_id = OBJECT_ID('dbo.Quizzes')
)
BEGIN
    CREATE INDEX IX_Quizzes_IsActive_IsPublished
        ON dbo.Quizzes(IsActive, IsPublished, QuizID);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_QuizAttempts_UserID_IsTestData_Status_QuizID'
      AND object_id = OBJECT_ID('dbo.QuizAttempts')
)
BEGIN
    CREATE INDEX IX_QuizAttempts_UserID_IsTestData_Status_QuizID
        ON dbo.QuizAttempts(UserID, IsTestData, Status, QuizID)
        INCLUDE (CompletedAt, StartedAt, ScorePercent);
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_ReportingLanguageSummary_RebuildSummary
AS
BEGIN
    SET NOCOUNT ON;

    TRUNCATE TABLE dbo.ReportingLanguageSummaryCache;

    INSERT INTO dbo.ReportingLanguageSummaryCache
    (
        LanguageCode,
        LanguageName,
        InProgressCount,
        CompletedCount,
        TotalUsers,
        LastRefreshedAt
    )
    SELECT
        c.PreferredLanguage,
        c.LanguageName,
        SUM(CASE WHEN c.IsInProgress = 1 THEN 1 ELSE 0 END) AS InProgressCount,
        SUM(CASE WHEN c.IsCompleted = 1 THEN 1 ELSE 0 END) AS CompletedCount,
        COUNT(*) AS TotalUsers,
        SYSDATETIMEOFFSET() AS LastRefreshedAt
    FROM dbo.ReportingUserLanguageCompletionCache c
    GROUP BY
        c.PreferredLanguage,
        c.LanguageName;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_ReportingLanguageSummary_RefreshUsers
    @UserIds dbo.GuidList READONLY
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM @UserIds)
        RETURN;

    DECLARE @GrandfatherCutoff DATETIMEOFFSET(3) = '2026-01-01T00:00:00+00:00';
    DECLARE @GrandfatherPassedModulesRequired INT = 9;
    DECLARE @PublishedModuleCount INT;

    SELECT
        @PublishedModuleCount = COUNT(*)
    FROM dbo.Quizzes q
    WHERE q.IsActive = 1
      AND q.IsPublished = 1;

    DELETE c
    FROM dbo.ReportingUserLanguageCompletionCache c
    INNER JOIN @UserIds u
      ON u.Id = c.UserID;

    ;WITH TargetUsers AS
    (
        SELECT
            u.UserID,
            PreferredLanguage = LOWER(LTRIM(RTRIM(COALESCE(NULLIF(u.PreferredLanguage, ''), 'en'))))
        FROM dbo.Users u
        INNER JOIN @UserIds ids
          ON ids.Id = u.UserID
        WHERE u.IsActive = 1
    ),
    PassedModules AS
    (
        SELECT
            a.UserID,
            PassedPublishedModuleCount = COUNT(DISTINCT a.QuizID),
            LastAttemptAt = MAX(COALESCE(a.CompletedAt, a.StartedAt))
        FROM dbo.QuizAttempts a
        INNER JOIN TargetUsers tu
          ON tu.UserID = a.UserID
        INNER JOIN dbo.Quizzes q
          ON q.QuizID = a.QuizID
        WHERE a.IsTestData = 0
          AND a.Status = 'Passed'
          AND q.IsActive = 1
          AND q.IsPublished = 1
        GROUP BY
            a.UserID
    ),
    GrandfatheredModules AS
    (
        SELECT
            a.UserID,
            GrandfatheredPassedModuleCount = COUNT(DISTINCT a.QuizID)
        FROM dbo.QuizAttempts a
        INNER JOIN TargetUsers tu
          ON tu.UserID = a.UserID
        INNER JOIN dbo.Quizzes q
          ON q.QuizID = a.QuizID
        WHERE a.IsTestData = 0
          AND a.Status = 'Passed'
          AND COALESCE(a.CompletedAt, a.StartedAt) < @GrandfatherCutoff
          AND q.IsActive = 1
          AND q.IsPublished = 1
        GROUP BY
            a.UserID
    )
    INSERT INTO dbo.ReportingUserLanguageCompletionCache
    (
        UserID,
        PreferredLanguage,
        LanguageName,
        PublishedModuleCount,
        PassedPublishedModuleCount,
        GrandfatheredPassedModuleCount,
        IsGrandfathered,
        IsCompleted,
        IsInProgress,
        LastAttemptAt,
        LastCalculatedAt
    )
    SELECT
        tu.UserID,
        tu.PreferredLanguage,
        CASE tu.PreferredLanguage
            WHEN 'en' THEN N'English'
            WHEN 'fr' THEN N'French'
            WHEN 'de' THEN N'German'
            WHEN 'pt' THEN N'Portuguese'
            WHEN 'es' THEN N'Spanish'
            ELSE UPPER(tu.PreferredLanguage)
        END AS LanguageName,
        @PublishedModuleCount AS PublishedModuleCount,
        ISNULL(pm.PassedPublishedModuleCount, 0) AS PassedPublishedModuleCount,
        ISNULL(gm.GrandfatheredPassedModuleCount, 0) AS GrandfatheredPassedModuleCount,
        CAST(CASE
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 1
            ELSE 0
        END AS BIT) AS IsGrandfathered,
        CAST(CASE
            WHEN @PublishedModuleCount > 0
             AND ISNULL(pm.PassedPublishedModuleCount, 0) = @PublishedModuleCount THEN 1
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 1
            ELSE 0
        END AS BIT) AS IsCompleted,
        CAST(CASE
            WHEN @PublishedModuleCount <= 0 THEN 0
            WHEN ISNULL(pm.PassedPublishedModuleCount, 0) = @PublishedModuleCount THEN 0
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 0
            ELSE 1
        END AS BIT) AS IsInProgress,
        pm.LastAttemptAt,
        SYSDATETIMEOFFSET() AS LastCalculatedAt
    FROM TargetUsers tu
    LEFT JOIN PassedModules pm
      ON pm.UserID = tu.UserID
    LEFT JOIN GrandfatheredModules gm
      ON gm.UserID = tu.UserID;

    EXEC dbo.SP_ReportingLanguageSummary_RebuildSummary;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_ReportingLanguageSummary_RebuildAll
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @GrandfatherCutoff DATETIMEOFFSET(3) = '2026-01-01T00:00:00+00:00';
    DECLARE @GrandfatherPassedModulesRequired INT = 9;
    DECLARE @PublishedModuleCount INT;

    SELECT
        @PublishedModuleCount = COUNT(*)
    FROM dbo.Quizzes q
    WHERE q.IsActive = 1
      AND q.IsPublished = 1;

    TRUNCATE TABLE dbo.ReportingUserLanguageCompletionCache;

    ;WITH TargetUsers AS
    (
        SELECT
            u.UserID,
            PreferredLanguage = LOWER(LTRIM(RTRIM(COALESCE(NULLIF(u.PreferredLanguage, ''), 'en'))))
        FROM dbo.Users u
        WHERE u.IsActive = 1
    ),
    PassedModules AS
    (
        SELECT
            a.UserID,
            PassedPublishedModuleCount = COUNT(DISTINCT a.QuizID),
            LastAttemptAt = MAX(COALESCE(a.CompletedAt, a.StartedAt))
        FROM dbo.QuizAttempts a
        INNER JOIN TargetUsers tu
          ON tu.UserID = a.UserID
        INNER JOIN dbo.Quizzes q
          ON q.QuizID = a.QuizID
        WHERE a.IsTestData = 0
          AND a.Status = 'Passed'
          AND q.IsActive = 1
          AND q.IsPublished = 1
        GROUP BY
            a.UserID
    ),
    GrandfatheredModules AS
    (
        SELECT
            a.UserID,
            GrandfatheredPassedModuleCount = COUNT(DISTINCT a.QuizID)
        FROM dbo.QuizAttempts a
        INNER JOIN TargetUsers tu
          ON tu.UserID = a.UserID
        INNER JOIN dbo.Quizzes q
          ON q.QuizID = a.QuizID
        WHERE a.IsTestData = 0
          AND a.Status = 'Passed'
          AND COALESCE(a.CompletedAt, a.StartedAt) < @GrandfatherCutoff
          AND q.IsActive = 1
          AND q.IsPublished = 1
        GROUP BY
            a.UserID
    )
    INSERT INTO dbo.ReportingUserLanguageCompletionCache
    (
        UserID,
        PreferredLanguage,
        LanguageName,
        PublishedModuleCount,
        PassedPublishedModuleCount,
        GrandfatheredPassedModuleCount,
        IsGrandfathered,
        IsCompleted,
        IsInProgress,
        LastAttemptAt,
        LastCalculatedAt
    )
    SELECT
        tu.UserID,
        tu.PreferredLanguage,
        CASE tu.PreferredLanguage
            WHEN 'en' THEN N'English'
            WHEN 'fr' THEN N'French'
            WHEN 'de' THEN N'German'
            WHEN 'pt' THEN N'Portuguese'
            WHEN 'es' THEN N'Spanish'
            ELSE UPPER(tu.PreferredLanguage)
        END AS LanguageName,
        @PublishedModuleCount AS PublishedModuleCount,
        ISNULL(pm.PassedPublishedModuleCount, 0) AS PassedPublishedModuleCount,
        ISNULL(gm.GrandfatheredPassedModuleCount, 0) AS GrandfatheredPassedModuleCount,
        CAST(CASE
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 1
            ELSE 0
        END AS BIT) AS IsGrandfathered,
        CAST(CASE
            WHEN @PublishedModuleCount > 0
             AND ISNULL(pm.PassedPublishedModuleCount, 0) = @PublishedModuleCount THEN 1
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 1
            ELSE 0
        END AS BIT) AS IsCompleted,
        CAST(CASE
            WHEN @PublishedModuleCount <= 0 THEN 0
            WHEN ISNULL(pm.PassedPublishedModuleCount, 0) = @PublishedModuleCount THEN 0
            WHEN ISNULL(gm.GrandfatheredPassedModuleCount, 0) >= @GrandfatherPassedModulesRequired THEN 0
            ELSE 1
        END AS BIT) AS IsInProgress,
        pm.LastAttemptAt,
        SYSDATETIMEOFFSET() AS LastCalculatedAt
    FROM TargetUsers tu
    LEFT JOIN PassedModules pm
      ON pm.UserID = tu.UserID
    LEFT JOIN GrandfatheredModules gm
      ON gm.UserID = tu.UserID;

    EXEC dbo.SP_ReportingLanguageSummary_RebuildSummary;
END;
GO

CREATE OR ALTER PROCEDURE dbo.SP_ReportingLanguageSummary_Get
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        s.LanguageCode,
        s.LanguageName,
        s.InProgressCount,
        s.CompletedCount,
        s.TotalUsers,
        s.LastRefreshedAt,
        CAST(0 AS INT) AS SortOrder
    FROM dbo.ReportingLanguageSummaryCache s

    UNION ALL

    SELECT
        CAST('zz' AS CHAR(2)) AS LanguageCode,
        N'Totals' AS LanguageName,
        COALESCE(SUM(s.InProgressCount), 0) AS InProgressCount,
        COALESCE(SUM(s.CompletedCount), 0) AS CompletedCount,
        COALESCE(SUM(s.TotalUsers), 0) AS TotalUsers,
        MAX(s.LastRefreshedAt) AS LastRefreshedAt,
        CAST(1 AS INT) AS SortOrder
    FROM dbo.ReportingLanguageSummaryCache s

    ORDER BY
        SortOrder,
        LanguageName;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_QuizAttempts_RefreshLanguageSummaryCache
ON dbo.QuizAttempts
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UserIds dbo.GuidList;

    INSERT INTO @UserIds (Id)
    SELECT DISTINCT UserID
    FROM inserted
    WHERE UserID IS NOT NULL

    UNION

    SELECT DISTINCT UserID
    FROM deleted
    WHERE UserID IS NOT NULL;

    IF EXISTS (SELECT 1 FROM @UserIds)
        EXEC dbo.SP_ReportingLanguageSummary_RefreshUsers @UserIds = @UserIds;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Users_RefreshLanguageSummaryCache
ON dbo.Users
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted)
       AND EXISTS (SELECT 1 FROM deleted)
       AND NOT (UPDATE(PreferredLanguage) OR UPDATE(IsActive) OR UPDATE(RoleCode))
        RETURN;

    DECLARE @UserIds dbo.GuidList;

    INSERT INTO @UserIds (Id)
    SELECT DISTINCT UserID
    FROM inserted
    WHERE UserID IS NOT NULL

    UNION

    SELECT DISTINCT UserID
    FROM deleted
    WHERE UserID IS NOT NULL;

    IF EXISTS (SELECT 1 FROM @UserIds)
        EXEC dbo.SP_ReportingLanguageSummary_RefreshUsers @UserIds = @UserIds;
END;
GO

CREATE OR ALTER TRIGGER dbo.TR_Quizzes_RefreshLanguageSummaryCache
ON dbo.Quizzes
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM inserted)
       AND EXISTS (SELECT 1 FROM deleted)
       AND NOT (UPDATE(IsPublished) OR UPDATE(IsActive))
        RETURN;

    EXEC dbo.SP_ReportingLanguageSummary_RebuildAll;
END;
GO

EXEC dbo.SP_ReportingLanguageSummary_RebuildAll;
GO
