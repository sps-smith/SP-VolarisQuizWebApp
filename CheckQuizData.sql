-- CheckQuizData.sql
-- Run in Azure Portal Query Editor to see what data exists

-- Show all quizzes and question counts
SELECT 
    q.DisplayOrder,
    qt.Title,
    COUNT(qu.QuestionID) AS QuestionCount
FROM Quizzes q
LEFT JOIN QuizTranslations qt ON qt.QuizID = q.QuizID AND qt.LanguageCode = 'en'
LEFT JOIN Questions qu ON qu.QuizID = q.QuizID AND qu.IsActive = 1
GROUP BY q.DisplayOrder, qt.Title
ORDER BY q.DisplayOrder;

-- Show question and option counts per module
SELECT 
    q.DisplayOrder AS Module,
    COUNT(DISTINCT qu.QuestionID) AS Questions,
    COUNT(DISTINCT ao.OptionID) AS Options,
    COUNT(DISTINCT aot.OptionID) AS OptionTranslations,
    COUNT(DISTINCT qt2.QuestionID) AS QuestionTranslations
FROM Quizzes q
LEFT JOIN Questions qu ON qu.QuizID = q.QuizID AND qu.IsActive = 1
LEFT JOIN AnswerOptions ao ON ao.QuestionID = qu.QuestionID
LEFT JOIN AnswerOptionTranslations aot ON aot.OptionID = ao.OptionID
LEFT JOIN QuestionTranslations qt2 ON qt2.QuestionID = qu.QuestionID
GROUP BY q.DisplayOrder
ORDER BY q.DisplayOrder;
