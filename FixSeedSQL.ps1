# FixSeedSQL.ps1
# Adds explicit column names to all shorthand INSERT statements in SeedQuestionsOnly.sql
# Run from the project root: .\FixSeedSQL.ps1

$file = ".\SeedQuestionsOnly.sql"
$content = Get-Content $file -Raw -Encoding UTF8

# Fix QuestionTranslations
$content = $content -replace `
    'INSERT INTO QuestionTranslations VALUES \(@QuestionID,', `
    'INSERT INTO QuestionTranslations (QuestionID,LanguageCode,QuestionText) VALUES (@QuestionID,'

# Fix AnswerOptions
$content = $content -replace `
    'INSERT INTO AnswerOptions VALUES\(@OptionID,@QuestionID,(\d+),(\d+)\)', `
    'INSERT INTO AnswerOptions (OptionID,QuestionID,DisplayOrder,IsCorrect) VALUES(@OptionID,@QuestionID,$1,$2)'

# Fix AnswerOptionTranslations
$content = $content -replace `
    'INSERT INTO AnswerOptionTranslations VALUES\(@OptionID,', `
    'INSERT INTO AnswerOptionTranslations (OptionID,LanguageCode,OptionText) VALUES(@OptionID,'

Set-Content $file $content -Encoding UTF8
Write-Host "Done. All INSERT statements now have explicit column names." -ForegroundColor Green
