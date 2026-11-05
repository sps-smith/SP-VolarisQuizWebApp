-- ============================================================
-- Volaris 101 — Full Quiz Seed (English, all 10 modules)
-- Run once against VolarisQuizDB
-- Safe to re-run — uses IF NOT EXISTS checks
-- ============================================================
SET NOCOUNT ON;

DECLARE @QuizID UNIQUEIDENTIFIER;
DECLARE @QuestionID UNIQUEIDENTIFIER;
DECLARE @OptionID UNIQUEIDENTIFIER;

-- ── Module 1: History of Volaris Group ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 1)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b815e';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuizID, 1, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations (QuizID, LanguageCode, Title)
    VALUES (@QuizID, 'en', N'History of Volaris Group');

    -- Q1
    SET @QuestionID = 'a1000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuestionID, @QuizID, 1, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations (QuestionID, LanguageCode, QuestionText)
    VALUES (@QuestionID, 'en', N'What is ROIC (Return on Invested Capital), in simple terms?');
    SET @OptionID = 'b1000001-0000-0000-0000-000000000001';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 1, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'A calculation that describes where capital is invested by a company.');
    SET @OptionID = 'b1000001-0000-0000-0000-000000000002';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 2, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'A calculation that measures how efficiently a company uses its capital to generate profit.');
    SET @OptionID = 'b1000001-0000-0000-0000-000000000003';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 3, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'A calculation of profit.');

    -- Q2
    SET @QuestionID = 'a1000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations (QuestionID, LanguageCode, QuestionText)
    VALUES (@QuestionID, 'en', N'What are the 2 types of growth?');
    SET @OptionID = 'b1000002-0000-0000-0000-000000000001';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 1, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'Initiatives or core.');
    SET @OptionID = 'b1000002-0000-0000-0000-000000000002';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 2, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'Initiatives or acquired.');
    SET @OptionID = 'b1000002-0000-0000-0000-000000000003';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 3, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'Organic or acquired.');

    -- Q3
    SET @QuestionID = 'a1000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations (QuestionID, LanguageCode, QuestionText)
    VALUES (@QuestionID, 'en', N'What is one of the reasons that small businesses are better for Volaris to acquire?');
    SET @OptionID = 'b1000003-0000-0000-0000-000000000001';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 1, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'They are easier to buy.');
    SET @OptionID = 'b1000003-0000-0000-0000-000000000002';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 2, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'They allow us to be a dominant player in specific vertical markets.');
    SET @OptionID = 'b1000003-0000-0000-0000-000000000003';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 3, 0);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'They allow us to organize businesses around products.');

    -- Q4 (multi-select)
    SET @QuestionID = 'a1000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuestionID, @QuizID, 4, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations (QuestionID, LanguageCode, QuestionText)
    VALUES (@QuestionID, 'en', N'Thinking about Porter''s 5 Forces, what are some of the benefits of being in a vertical market? (Select all that apply)');
    SET @OptionID = 'b1000004-0000-0000-0000-000000000001';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 1, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'Competitors will have high barriers to entry because switching costs are high and initial development costs are high.');
    SET @OptionID = 'b1000004-0000-0000-0000-000000000002';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 2, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'In general, we don''t face Supplier pressure, since development resources are usually available.');
    SET @OptionID = 'b1000004-0000-0000-0000-000000000003';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 3, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'Substitution pressure is low. Often, Excel is our primary competitor for solutions in a vertical.');
    SET @OptionID = 'b1000004-0000-0000-0000-000000000004';
    INSERT INTO AnswerOptions (OptionID, QuestionID, DisplayOrder, IsCorrect) VALUES (@OptionID, @QuestionID, 4, 1);
    INSERT INTO AnswerOptionTranslations (OptionID, LanguageCode, OptionText) VALUES (@OptionID, 'en', N'All of the above.');

END
ELSE PRINT 'Module 1 already exists — skipped.';

-- ── Module 2: Financials 101 ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 2)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b815f';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuizID, 2, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations (QuizID, LanguageCode, Title)
    VALUES (@QuizID, 'en', N'Financials 101');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt)
    VALUES (@QuestionID, @QuizID, 1, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations (QuestionID, LanguageCode, QuestionText) VALUES (@QuestionID, 'en', N'What is the target operating ratio for Core?');
    SET @OptionID = 'b2000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'45%');
    SET @OptionID = 'b2000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'60%');
    SET @OptionID = 'b2000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'30%');
    SET @OptionID = 'b2000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'50%');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Once the Core ratio target is achieved, businesses can:');
    SET @OptionID = 'b2000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Deploy capital to acquisitions and/or initiatives.');
    SET @OptionID = 'b2000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Hire more people.');
    SET @OptionID = 'b2000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Increase maintenance pricing.');
    SET @OptionID = 'b2000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'A and C.');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is working capital?');
    SET @OptionID = 'b2000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Net revenue minus operating costs.');
    SET @OptionID = 'b2000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Current assets minus depreciation.');
    SET @OptionID = 'b2000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Current assets minus current liabilities.');
    SET @OptionID = 'b2000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Net revenue plus current assets.');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'In hardware & services delivery, the Terms & Conditions agreement can have a critical impact on working capital. Which of these will NOT help working capital?');
    SET @OptionID = 'b2000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'It''s an opportunity to get money up front.');
    SET @OptionID = 'b2000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Can set shorter invoice terms, to get money in sooner in the project.');
    SET @OptionID = 'b2000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Can allow the customer to choose payment conditions.');
    SET @OptionID = 'b2000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Can set milestones to mimic a monthly payment cycle.');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'The total value of the bookings will be equal to the total gross revenue recognized at contract completion.');
    SET @OptionID = 'b2000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'True');
    SET @OptionID = 'b2000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'False');

    SET @QuestionID = 'a2000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'For maintenance revenues, which one factor has the highest impact on increasing working capital?');
    SET @OptionID = 'b2000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Collect cash on time.');
    SET @OptionID = 'b2000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Bill in advance.');
    SET @OptionID = 'b2000006-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Excellent customer care.');
    SET @OptionID = 'b2000006-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Work closely with collection teams.');

END
ELSE PRINT 'Module 2 already exists — skipped.';

-- ── Module 3: Sales & Marketing ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 3)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8160';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 3, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Sales & Marketing');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is the value wedge?');
    SET @OptionID = 'b3000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'What we say and what we sell.');
    SET @OptionID = 'b3000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The difference between what we say and what we sell.');
    SET @OptionID = 'b3000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The sweet spot where key differentiators, when compared to your competition, match the perceived value of a prospect.');
    SET @OptionID = 'b3000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The sweet spot where products and content match a requirements list.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'If you can change the conversation to focus on the value wedge, and gain the power position, what are the benefits? (Select all that apply)');
    SET @OptionID = 'b3000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'You can deliver things that are important to the customer');
    SET @OptionID = 'b3000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'You can quickly answer their checklist of features');
    SET @OptionID = 'b3000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'You can deliver things that are differentiators (unique to your organization)');
    SET @OptionID = 'b3000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'You can talk about things that are defendable through customer stories.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Having a deep understanding of your customer is critical to achieving a strong power position. Asking why to arrive at meaning and to answer the "why change" question is called:');
    SET @OptionID = 'b3000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Commercial insight');
    SET @OptionID = 'b3000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Requirements analysis');
    SET @OptionID = 'b3000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Discovery');
    SET @OptionID = 'b3000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Power Pyramid.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following are key differences between Inbound Marketing and Outbound Marketing?');
    SET @OptionID = 'b3000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Inbound is 2-way, while Outbound is mostly 1-way');
    SET @OptionID = 'b3000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Inbound is more focused on education, while Outbound is more focused on events and design');
    SET @OptionID = 'b3000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Inbound is active in the revenue cycle, while Outbound is not');
    SET @OptionID = 'b3000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Inbound uses strong relationships, content and digital strategies to get customers to come to them; while Outbound tries to target customers "out there"');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'A document that describes the background, responsibilities, challenges, and preferred content is called a:');
    SET @OptionID = 'b3000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Buyer persona');
    SET @OptionID = 'b3000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Buyer journey');
    SET @OptionID = 'b3000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Content plan.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is key benefit of aligning content to a buyer''s journey:');
    SET @OptionID = 'b3000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'It makes it easier to align product information to personas');
    SET @OptionID = 'b3000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'It starts the conversation with a potential buyer while they are still in the research phase of their journey');
    SET @OptionID = 'b3000006-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'It speeds up the Sales cycle.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000007';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 7, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Customer obsession needs to be part of company culture. Which description shows how this should be, from a Sales & Marketing perspective?');
    SET @OptionID = 'b3000007-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Focusing on representing the customer, targeting them individually based on their journey, developing continuous relationships across many channels, and using data to make fact-based decisions.');
    SET @OptionID = 'b3000007-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Focusing on representing the company, targeting key demographics, delivering point-in-time communications, and using the data you can get to make intuitive decisions.');

    SET @QuestionID = 'a3000001-0000-0000-0000-000000000008';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 8, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When does an MQL (Marketing Qualified Lead) become an SQL (Sales Qualified Lead)?');
    SET @OptionID = 'b3000008-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a nurturing campaign ends');
    SET @OptionID = 'b3000008-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a prospect completes an inquiry form on the company website');
    SET @OptionID = 'b3000008-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a prospect demonstrates certain behaviours that indicate they are ready to buy');
    SET @OptionID = 'b3000008-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a specific period of time has elapsed (e.g. 3 months).');

END
ELSE PRINT 'Module 3 already exists — skipped.';

-- ── Module 4: Recurring Revenue and Pricing ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 4)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8161';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 4, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Recurring Revenue and Pricing');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Why is recurring revenue the best type of revenue stream for Volaris companies? (Select all that apply)');
    SET @OptionID = 'b4000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Management can spend more time growing the business.');
    SET @OptionID = 'b4000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Pricing is usually industry-standard.');
    SET @OptionID = 'b4000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'It''s usually the largest profit margin.');
    SET @OptionID = 'b4000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Recurring revenue is more predictable and dependable than license revenues.');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Best practice businesses achieve a minimum ____% increase in recurring revenues each year:');
    SET @OptionID = 'b4000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'12%');
    SET @OptionID = 'b4000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'10%');
    SET @OptionID = 'b4000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'5%');
    SET @OptionID = 'b4000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'15%');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Value-based pricing is a strategy to set pricing with consideration for customers'' perceived value and willingness to pay for the good or service. True or false?');
    SET @OptionID = 'b4000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'True');
    SET @OptionID = 'b4000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'False');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is "share of wallet" when talking about recurring revenue?');
    SET @OptionID = 'b4000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Percentage revenues that a specific customer contributes to a company''s total revenues.');
    SET @OptionID = 'b4000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Total revenues as a percentage of a customer''s total operating budget.');
    SET @OptionID = 'b4000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Total licensing cost for a company''s solution as a percentage of a customer''s total operating budget.');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is the relationship between switching costs and maintenance pricing?');
    SET @OptionID = 'b4000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'There is no relationship.');
    SET @OptionID = 'b4000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The lower the switching costs, the more you can charge for maintenance.');
    SET @OptionID = 'b4000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The higher the switching costs, the more you can charge for maintenance.');
    SET @OptionID = 'b4000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The higher the switching costs, the less you can charge for maintenance.');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'How should you set pricing, to maximize recurring revenue?');
    SET @OptionID = 'b4000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Determine maintenance rates, then work backwards to calculate license pricing.');
    SET @OptionID = 'b4000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Set recurring revenues at 15% of list license fees.');
    SET @OptionID = 'b4000006-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Price maintenance rates and licenses separately.');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000007';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 7, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which factors should you consider going into a pricing review?');
    SET @OptionID = 'b4000007-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Risk of attrition');
    SET @OptionID = 'b4000007-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'General market conditions');
    SET @OptionID = 'b4000007-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Intensity of competition');
    SET @OptionID = 'b4000007-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above');

    SET @QuestionID = 'a4000001-0000-0000-0000-000000000008';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 8, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When is the best time to initiate a maintenance price increase? (Select all that apply)');
    SET @OptionID = 'b4000008-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a contract comes up for renewal.');
    SET @OptionID = 'b4000008-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'After a major product release.');
    SET @OptionID = 'b4000008-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Shortly after a company has been acquired');

END
ELSE PRINT 'Module 4 already exists — skipped.';

-- ── Module 5: Professional Services ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 5)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8162';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 5, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Professional Services');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What are the top 3 things that can improve the Professional Services ratio?');
    SET @OptionID = 'b5000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Training, commitment and reporting.');
    SET @OptionID = 'b5000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Forecast, resources and delivery.');
    SET @OptionID = 'b5000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Costs, reporting and forecast.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is white space?');
    SET @OptionID = 'b5000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Hours/days in your calendar when no billable activities are scheduled.');
    SET @OptionID = 'b5000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Hours/days in your calendar when you are assigned to training.');
    SET @OptionID = 'b5000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Hours/days in your calendar after you have already met revenue targets.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'How often should white space be reviewed and managed?');
    SET @OptionID = 'b5000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Daily');
    SET @OptionID = 'b5000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Weekly');
    SET @OptionID = 'b5000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Monthly');
    SET @OptionID = 'b5000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Quarterly');
    SET @OptionID = 'b5000003-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Annually.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Capacity planning should be done at least once/quarter. What should you look for? (Select all that apply)');
    SET @OptionID = 'b5000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Big increase or decrease in forecast capacity in the next month.');
    SET @OptionID = 'b5000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Big increase or decrease in forecast capacity over the next year.');
    SET @OptionID = 'b5000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Knowledge/skill gaps for confirmed projects.');
    SET @OptionID = 'b5000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Lead time to hire contractors or outsource in case of a spike in capacity for a limited period of time.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When it comes to Professional Services resources, time is the most perishable resource we have. What does NOT negatively impact/reduce utilization, and your PS operating ratio?');
    SET @OptionID = 'b5000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Project delays');
    SET @OptionID = 'b5000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Employee turnover');
    SET @OptionID = 'b5000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Which tool you use for forecasting');
    SET @OptionID = 'b5000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Project overruns');
    SET @OptionID = 'b5000005-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Double-booked or under-booked resources.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'In one month, the PS team had the following results. Select the person who had the highest billability:');
    SET @OptionID = 'b5000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Avi worked 10 billable days, and 10 paid time-off days.');
    SET @OptionID = 'b5000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Jacinda worked 15 billable days, had 3 admin days and 2 non-billable project days.');
    SET @OptionID = 'b5000006-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Bob worked 15 billable days, had 5 paid time-off days and 5 non-billable project days.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000007';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 7, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'At the end of a PS project, you should:');
    SET @OptionID = 'b5000007-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Move on to the next project.');
    SET @OptionID = 'b5000007-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Get a formal signoff from the customer, then recognize license revenue and any deferred revenue.');
    SET @OptionID = 'b5000007-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Plan training for the next quarter.');

    SET @QuestionID = 'a5000001-0000-0000-0000-000000000008';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 8, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of these strategies does NOT keep projects at full throttle?');
    SET @OptionID = 'b5000008-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Improve run rate');
    SET @OptionID = 'b5000008-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Close POs for disengaged customers');
    SET @OptionID = 'b5000008-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Fill white space');
    SET @OptionID = 'b5000008-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Maximize revenue recognition');
    SET @OptionID = 'b5000008-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Improve capacity planning');

END
ELSE PRINT 'Module 5 already exists — skipped.';

-- ── Module 6: Research & Development ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 6)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8163';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 6, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Research & Development');

    SET @QuestionID = 'a6000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following are generally true about our customers? (Select all that apply)');
    SET @OptionID = 'b6000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Mobile computing is really important for all apps.');
    SET @OptionID = 'b6000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Multi-platform products are really important.');
    SET @OptionID = 'b6000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'They are technology early adopters.');
    SET @OptionID = 'b6000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above.');
    SET @OptionID = 'b6000001-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'None of the above.');

    SET @QuestionID = 'a6000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'In general, code rewrites and new platforms are cost-effective.');
    SET @OptionID = 'b6000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'True');
    SET @OptionID = 'b6000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'False');

    SET @QuestionID = 'a6000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When thinking about a code rewrite/new platform, which of the following is usually overestimated? (Select all that apply)');
    SET @OptionID = 'b6000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Number of customers who will switch/pay for the switch');
    SET @OptionID = 'b6000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Number of developers required');
    SET @OptionID = 'b6000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Time to complete the project');
    SET @OptionID = 'b6000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Number of new customers.');

    SET @QuestionID = 'a6000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Why is it hard for vertical market products to go global?');
    SET @OptionID = 'b6000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'You can end up with a lowest common denominator product (trying for one product fits all).');
    SET @OptionID = 'b6000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Requirements and workflows can be very different in different regions.');
    SET @OptionID = 'b6000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'There are hidden costs associated with each new region.');
    SET @OptionID = 'b6000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above');
    SET @OptionID = 'b6000004-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'A and C only.');

    SET @QuestionID = 'a6000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Why might you include technical debt planning in your R&D budget? (Select all that apply)');
    SET @OptionID = 'b6000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Addressing technical debt on new features');
    SET @OptionID = 'b6000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Getting customers to modernize by moving to mobile platforms');
    SET @OptionID = 'b6000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Replatforming or rewriting code');
    SET @OptionID = 'b6000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Developing a new product');

END
ELSE PRINT 'Module 6 already exists — skipped.';

-- ── Module 7: Talent Management ───────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 7)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8164';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 7, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Talent Management');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What are the key goals of performance coaching? (Select all that apply)');
    SET @OptionID = 'b7000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To identify individual training needs.');
    SET @OptionID = 'b7000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To manage employee benefits.');
    SET @OptionID = 'b7000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To build autonomy and accountability.');
    SET @OptionID = 'b7000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To generate higher engagement and increase job satisfaction.');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What are the objectives of talent management at Volaris?');
    SET @OptionID = 'b7000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Enable the organization to attract and retain top talent.');
    SET @OptionID = 'b7000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Actively develop people aligning their career aspirations with company resource requirements.');
    SET @OptionID = 'b7000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Embed an inclusive workplace culture.');
    SET @OptionID = 'b7000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above.');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What do SMART goals and objectives stand for?');
    SET @OptionID = 'b7000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Specific, Metrics, Agreement, Relevant, Time-bound.');
    SET @OptionID = 'b7000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Successful, Measurable, Attainable, Relevant, Time-bound.');
    SET @OptionID = 'b7000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Specific, Measurable, Attainable, Relevant, Time-bound.');
    SET @OptionID = 'b7000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Specific, Measurable, Attainable, Repeatable, Time-bound.');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When creating SMART goals and objectives, which of the following is TRUE? (Select all that apply)');
    SET @OptionID = 'b7000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Should focus on role-specific outcomes.');
    SET @OptionID = 'b7000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Should focus on personal development goals.');
    SET @OptionID = 'b7000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Should have no more than 1-2 goals.');
    SET @OptionID = 'b7000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Should focus on job responsibilities.');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What are the main elements of compensation: (Select all that apply)');
    SET @OptionID = 'b7000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Wellbeing');
    SET @OptionID = 'b7000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Job design');
    SET @OptionID = 'b7000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Recognition');
    SET @OptionID = 'b7000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Employee development');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following are key components of futureproofing talent and succession planning? (Select all that apply)');
    SET @OptionID = 'b7000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Clearly align on what good performance and potential look like.');
    SET @OptionID = 'b7000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Profile high potential talent to senior leaders.');
    SET @OptionID = 'b7000006-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Proactively manage succession risks and identify gaps in internal talent pipelines.');
    SET @OptionID = 'b7000006-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Accelerate the development of our internal talent to meet future leadership needs.');
    SET @OptionID = 'b7000006-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above');

    SET @QuestionID = 'a7000001-0000-0000-0000-000000000007';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 7, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'High potentials have strong engagement when… (select all that apply)');
    SET @OptionID = 'b7000007-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Current work aligns to their current work preferences.');
    SET @OptionID = 'b7000007-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'They define their own development needs.');
    SET @OptionID = 'b7000007-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'The mission, direction and values are aligned with the employee.');
    SET @OptionID = 'b7000007-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Provided with challenges based on high volume of work.');

END
ELSE PRINT 'Module 7 already exists — skipped.';

-- ── Module 8: Acquisitions ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 8)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8165';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 8, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Acquisitions');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which key messages are true, regarding the businesses we acquire? (Select all that apply)');
    SET @OptionID = 'b8000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Our niche is vertical markets.');
    SET @OptionID = 'b8000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Businesses we buy must build mission-critical products.');
    SET @OptionID = 'b8000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Businesses we buy should have a small customer base.');
    SET @OptionID = 'b8000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Businesses should have deep knowledge about their vertical.');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following is NOT a key driver for acquiring a company?');
    SET @OptionID = 'b8000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Deploy capital');
    SET @OptionID = 'b8000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Roll up smaller competitors');
    SET @OptionID = 'b8000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Expand geographically');
    SET @OptionID = 'b8000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Buy missing products or skill set.');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'ActionSoftware has $2000 in assets and $500 in liabilities. Their operating income is $150. The ROIC (Return on Invested Capital) is:');
    SET @OptionID = 'b8000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'5%');
    SET @OptionID = 'b8000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'10%');
    SET @OptionID = 'b8000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'15%');
    SET @OptionID = 'b8000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'20%.');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following factors can be a red flag when considering a company for acquisition? (Select all that apply)');
    SET @OptionID = 'b8000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'They have one large customer');
    SET @OptionID = 'b8000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'There are lots of regulatory changes in their customers'' industry');
    SET @OptionID = 'b8000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'They have a very small number of employees');
    SET @OptionID = 'b8000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'They are about the same size as their largest competitor');
    SET @OptionID = 'b8000004-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Your assessment shows low switching costs');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'When is market share less important as a factor?');
    SET @OptionID = 'b8000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a target company''s software is mission critical (barrier to entry for competitors).');
    SET @OptionID = 'b8000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'When a target company''s software has a lot of add-ons.');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Maintenance revenue is the most reliable way to evaluate revenue streams for forecasting on a target company:');
    SET @OptionID = 'b8000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'True');
    SET @OptionID = 'b8000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'False.');

    SET @QuestionID = 'a8000001-0000-0000-0000-000000000007';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 7, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Everyone can help accelerate M&A growth without distracting from their day job. How? (Select all that apply)');
    SET @OptionID = 'b8000007-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Sustain a strong core business');
    SET @OptionID = 'b8000007-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Identify new acquisition targets');
    SET @OptionID = 'b8000007-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Share interesting industry information');
    SET @OptionID = 'b8000007-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Evaluate potential companies to determine whether they''re good at acquisition targets');

END
ELSE PRINT 'Module 8 already exists — skipped.';

-- ── Module 9: Organic Growth ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 9)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8166';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 9, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'Organic Growth');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What does an organization need to focus on first, before focusing on organic growth?');
    SET @OptionID = 'b9000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Initiatives');
    SET @OptionID = 'b9000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'New customers');
    SET @OptionID = 'b9000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Core operating ratio');
    SET @OptionID = 'b9000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Maintenance revenue');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following statements about initiatives are true? (Select all that apply)');
    SET @OptionID = 'b9000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'May be programs that improve employee morale or customer service.');
    SET @OptionID = 'b9000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Must be measurable.');
    SET @OptionID = 'b9000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Must have at least one person accountable for it.');
    SET @OptionID = 'b9000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Can be cancelled if results aren''t as expected.');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following statements about experiments are true? (Select all that apply)');
    SET @OptionID = 'b9000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Must be tracked in the QSR.');
    SET @OptionID = 'b9000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Can be as short as a few hours or days.');
    SET @OptionID = 'b9000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Do not need to focus on growth.');
    SET @OptionID = 'b9000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Don''t require a SIG');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'From which quadrant for organic growth do most of our successful initiatives come?');
    SET @OptionID = 'b9000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Quadrant 1: Back-to-base');
    SET @OptionID = 'b9000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Quadrant 2: New products');
    SET @OptionID = 'b9000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Quadrant 3: New customers');
    SET @OptionID = 'b9000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Quadrant 4: New products & customers');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'An initiative champion should be: (Select all that apply)');
    SET @OptionID = 'b9000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Carrying out the initiative alongside their day-to-day responsibilities');
    SET @OptionID = 'b9000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Someone with a broad knowledge of different role types');
    SET @OptionID = 'b9000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Solely focused on the sales and marketing of the initiative');
    SET @OptionID = 'b9000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'A person with high emotional intelligence to speak to different levels of an organization');

    SET @QuestionID = 'a9000001-0000-0000-0000-000000000006';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 6, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following statements is true?');
    SET @OptionID = 'b9000006-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'A SIG is a group of customers that pay for an initiative in advance');
    SET @OptionID = 'b9000006-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'A SIG is a group of customers who volunteer time to help build your initiative');

END
ELSE PRINT 'Module 9 already exists — skipped.';

-- ── Module 10: AI ─────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Quizzes WHERE DisplayOrder = 10)
BEGIN
    SET @QuizID = '318f0a60-9b50-426b-8e03-ee03094b8167';
    INSERT INTO Quizzes (QuizID, DisplayOrder, PassingScore, IsActive, CreatedAt, UpdatedAt) VALUES (@QuizID, 10, 50.00, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuizTranslations VALUES (@QuizID, 'en', N'AI');

    SET @QuestionID = 'a0000001-0000-0000-0000-000000000001';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 1, 1, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following ways are Volaris businesses using AI? (Select all that apply)');
    SET @OptionID = 'c0000001-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To streamline workflows');
    SET @OptionID = 'c0000001-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To manage meeting notes');
    SET @OptionID = 'c0000001-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To share knowledge');
    SET @OptionID = 'c0000001-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'To improve R&D output & quality');
    SET @OptionID = 'c0000001-0000-0000-0000-000000000005'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 5, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'All of the above');

    SET @QuestionID = 'a0000001-0000-0000-0000-000000000002';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 2, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is a recommended approach for leaders at Volaris to support AI adoption?');
    SET @OptionID = 'c0000002-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Avoid experimentation to minimize risk');
    SET @OptionID = 'c0000002-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Provide an environment where people have permission to experiment');
    SET @OptionID = 'c0000002-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Rely solely on top-down solutions');
    SET @OptionID = 'c0000002-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Discourage sharing of best practices');

    SET @QuestionID = 'a0000001-0000-0000-0000-000000000003';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 3, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Which of the following is NOT an appropriate use of AI?');
    SET @OptionID = 'c0000003-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Drafting meeting notes');
    SET @OptionID = 'c0000003-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Managing project estimates');
    SET @OptionID = 'c0000003-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Making high-stakes decisions without human review');
    SET @OptionID = 'c0000003-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Streamlining workflows');

    SET @QuestionID = 'a0000001-0000-0000-0000-000000000004';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 4, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'What is the first stage in the AI maturity journey?');
    SET @OptionID = 'c0000004-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Adopters');
    SET @OptionID = 'c0000004-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Incrementalists');
    SET @OptionID = 'c0000004-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Skeptics');
    SET @OptionID = 'c0000004-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'Evangelists');

    SET @QuestionID = 'a0000001-0000-0000-0000-000000000005';
    INSERT INTO Questions (QuestionID, QuizID, DisplayOrder, IsMultiSelect, IsActive, CreatedAt, UpdatedAt) VALUES (@QuestionID, @QuizID, 5, 0, 1, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET());
    INSERT INTO QuestionTranslations VALUES (@QuestionID, 'en', N'Why is human review considered essential when using AI tools at Volaris?');
    SET @OptionID = 'c0000005-0000-0000-0000-000000000001'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 1, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'AI tools are always accurate');
    SET @OptionID = 'c0000005-0000-0000-0000-000000000002'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 2, 1); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'AI tools can hallucinate and produce incorrect outputs');
    SET @OptionID = 'c0000005-0000-0000-0000-000000000003'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 3, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'AI tools do not require compliance checks');
    SET @OptionID = 'c0000005-0000-0000-0000-000000000004'; INSERT INTO AnswerOptions VALUES (@OptionID, @QuestionID, 4, 0); INSERT INTO AnswerOptionTranslations VALUES (@OptionID, 'en', N'AI tools can handle sensitive data without oversight');

END
ELSE PRINT 'Module 10 already exists — skipped.';

PRINT 'Seed complete. All 10 modules inserted.';
