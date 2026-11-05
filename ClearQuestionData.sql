-- ClearQuestionData.sql
-- Deletes all question/option data but KEEPS the Quizzes and QuizTranslations
-- Run this if questions exist but have no translations or options
-- Then re-run SeedQuestionsOnly.sql

SET NOCOUNT ON;

-- Check counts before deleting
SELECT 'Before delete' AS Stage,
    (SELECT COUNT(*) FROM AttemptAnswers) AS AttemptAnswers,
    (SELECT COUNT(*) FROM QuizAttempts) AS QuizAttempts,
    (SELECT COUNT(*) FROM AnswerOptionTranslations) AS AnswerOptionTranslations,
    (SELECT COUNT(*) FROM AnswerOptions) AS AnswerOptions,
    (SELECT COUNT(*) FROM QuestionTranslations) AS QuestionTranslations,
    (SELECT COUNT(*) FROM Questions) AS Questions;

-- Delete in dependency order
DELETE FROM AttemptAnswers;
DELETE FROM QuizAttempts;
DELETE FROM AnswerOptionTranslations;
DELETE FROM AnswerOptions;
DELETE FROM QuestionTranslations;
DELETE FROM Questions;

SELECT 'After delete' AS Stage,
    (SELECT COUNT(*) FROM Questions) AS Questions,
    (SELECT COUNT(*) FROM AnswerOptions) AS AnswerOptions,
    (SELECT COUNT(*) FROM QuizAttempts) AS QuizAttempts;

PRINT 'Done. Now run SeedQuestionsOnly.sql to re-insert all questions.';
