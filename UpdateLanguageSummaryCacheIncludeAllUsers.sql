SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

/*
    Delta update: language dashboard should include all active users
    ---------------------------------------------------------------
    This script updates the existing stored procedures so the language
    dashboard cache is built from all active users, not only learners.
*/

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

EXEC dbo.SP_ReportingLanguageSummary_RebuildAll;
GO
