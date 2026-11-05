-- DiagnoseQuizLoad.sql
-- Check exactly what the app would see when loading quizzes

-- Are questions active?
SELECT 
    q.DisplayOrder AS Module,
    COUNT(CASE WHEN qu.IsActive = 1 THEN 1 END) AS ActiveQuestions,
    COUNT(CASE WHEN qu.IsActive = 0 THEN 1 END) AS InactiveQuestions
FROM Quizzes q
LEFT JOIN Questions qu ON qu.QuizID = q.QuizID
GROUP BY q.DisplayOrder
ORDER BY q.DisplayOrder;

-- Are quizzes active?
SELECT DisplayOrder, IsActive FROM Quizzes ORDER BY DisplayOrder;

-- Sample: first question of module 1 with its translation and options
SELECT TOP 5
    qu.QuestionID,
    qu.DisplayOrder,
    qu.IsActive,
    qu.IsMultiSelect,
    qt.QuestionText,
    ao.OptionID,
    ao.IsCorrect,
    aot.OptionText
FROM Quizzes qz
JOIN Questions qu ON qu.QuizID = qz.QuizID
JOIN QuestionTranslations qt ON qt.QuestionID = qu.QuestionID AND qt.LanguageCode = 'en'
JOIN AnswerOptions ao ON ao.QuestionID = qu.QuestionID
JOIN AnswerOptionTranslations aot ON aot.OptionID = ao.OptionID AND aot.LanguageCode = 'en'
WHERE qz.DisplayOrder = 1
ORDER BY qu.DisplayOrder, ao.DisplayOrder;
