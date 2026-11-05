IF COL_LENGTH('dbo.QuizAttempts', 'IsTestData') IS NULL
BEGIN
    ALTER TABLE dbo.QuizAttempts ADD IsTestData BIT NOT NULL
        CONSTRAINT DF_QuizAttempts_IsTestData DEFAULT 0;
END;

IF COL_LENGTH('dbo.QuizAttempts', 'RawScore') IS NULL
BEGIN
    ALTER TABLE dbo.QuizAttempts ADD RawScore INT NULL;
END;

IF COL_LENGTH('dbo.QuizAttempts', 'TotalQuestions') IS NULL
BEGIN
    ALTER TABLE dbo.QuizAttempts ADD TotalQuestions INT NULL;
END;

IF COL_LENGTH('dbo.QuizAttempts', 'CurrentQuestionPosition') IS NULL
BEGIN
    ALTER TABLE dbo.QuizAttempts ADD CurrentQuestionPosition INT NULL;
END;

IF OBJECT_ID('dbo.ReportingAttemptSummaryCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReportingAttemptSummaryCache (
        UserID        UNIQUEIDENTIFIER NOT NULL,
        QuizID        UNIQUEIDENTIFIER NOT NULL,
        IsTestData    BIT              NOT NULL,
        HasPassed     BIT              NOT NULL,
        TotalAttempts INT              NOT NULL,
        BestScore     DECIMAL(5,2)     NULL,
        LastAttemptAt DATETIMEOFFSET(3) NULL,
        CONSTRAINT PK_ReportingAttemptSummaryCache PRIMARY KEY (UserID, QuizID, IsTestData)
    );
END;

TRUNCATE TABLE dbo.ReportingAttemptSummaryCache;

INSERT INTO dbo.ReportingAttemptSummaryCache (UserID, QuizID, IsTestData, HasPassed, TotalAttempts, BestScore, LastAttemptAt)
SELECT
    a.UserID,
    a.QuizID,
    a.IsTestData,
    CAST(CASE WHEN SUM(CASE WHEN a.Status = 'Passed' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS HasPassed,
    COUNT(*) AS TotalAttempts,
    MAX(CASE WHEN a.Status = 'Passed' THEN a.ScorePercent END) AS BestScore,
    MAX(COALESCE(a.CompletedAt, a.StartedAt)) AS LastAttemptAt
FROM dbo.QuizAttempts a
WHERE a.Status <> 'InProgress'
GROUP BY a.UserID, a.QuizID, a.IsTestData;

EXEC('
CREATE OR ALTER TRIGGER dbo.TR_QuizAttempts_RefreshReportingAttemptSummaryCache
ON dbo.QuizAttempts
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Affected TABLE (
        UserID UNIQUEIDENTIFIER NOT NULL,
        QuizID UNIQUEIDENTIFIER NOT NULL,
        IsTestData BIT NOT NULL
    );

    INSERT INTO @Affected (UserID, QuizID, IsTestData)
    SELECT DISTINCT UserID, QuizID, IsTestData FROM inserted
    UNION
    SELECT DISTINCT UserID, QuizID, IsTestData FROM deleted;

    DELETE c
    FROM dbo.ReportingAttemptSummaryCache c
    INNER JOIN @Affected a
        ON a.UserID = c.UserID
       AND a.QuizID = c.QuizID
       AND a.IsTestData = c.IsTestData;

    INSERT INTO dbo.ReportingAttemptSummaryCache (UserID, QuizID, IsTestData, HasPassed, TotalAttempts, BestScore, LastAttemptAt)
    SELECT
        a.UserID,
        a.QuizID,
        a.IsTestData,
        CAST(CASE WHEN SUM(CASE WHEN a.Status = ''Passed'' THEN 1 ELSE 0 END) > 0 THEN 1 ELSE 0 END AS bit) AS HasPassed,
        COUNT(*) AS TotalAttempts,
        MAX(CASE WHEN a.Status = ''Passed'' THEN a.ScorePercent END) AS BestScore,
        MAX(COALESCE(a.CompletedAt, a.StartedAt)) AS LastAttemptAt
    FROM dbo.QuizAttempts a
    INNER JOIN @Affected af
        ON af.UserID = a.UserID
       AND af.QuizID = a.QuizID
       AND af.IsTestData = a.IsTestData
    WHERE a.Status <> ''InProgress''
    GROUP BY a.UserID, a.QuizID, a.IsTestData;
END;');
