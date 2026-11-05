namespace Volaris101.Domain.Entities;

public class User
{
    public Guid UserId { get; set; }
    public string Login { get; set; } = string.Empty;       // SAML NameID
    public string IdpSource { get; set; } = "Okta";
    public string IdpSubjectIdentifier { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? Company { get; set; }     // Okta: VBUName (customVBUName)
    public string? Portfolio { get; set; }
    public int? OrganizationId { get; set; }                 // Okta: VBUID (customVBUID)
    public int? PortfolioId { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public string RoleCode { get; set; } = "Learner";
    public DateTimeOffset AccountCreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    public DateTimeOffset? LastAttributeRefreshAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string Initials =>
        $"{(FirstName.Length > 0 ? FirstName[0] : ' ')}{(LastName.Length > 0 ? LastName[0] : ' ')}".Trim().ToUpper();
}

public class Quiz
{
    public Guid QuizId { get; set; }
    public int DisplayOrder { get; set; }
    public decimal PassingScore { get; set; } = 50m;
    public bool IsActive { get; set; } = true;
    public bool IsPublished { get; set; } = false;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<QuizTranslation> Translations { get; set; } = new();
    public List<Question> Questions { get; set; } = new();

    public string GetTitle(string lang = "en") =>
        Translations.FirstOrDefault(t => t.LanguageCode == lang)?.Title
        ?? Translations.FirstOrDefault(t => t.LanguageCode == "en")?.Title
        ?? $"Module {DisplayOrder}";
}

public class QuizTranslation
{
    public Guid QuizId { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string Title { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

public class Question
{
    public Guid QuestionId { get; set; }
    public Guid QuizId { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsMultiSelect { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<QuestionTranslation> Translations { get; set; } = new();
    public List<AnswerOption> Options { get; set; } = new();

    public string GetText(string lang = "en") =>
        Translations.FirstOrDefault(t => t.LanguageCode == lang)?.QuestionText
        ?? Translations.FirstOrDefault(t => t.LanguageCode == "en")?.QuestionText
        ?? string.Empty;
}

public class QuestionTranslation
{
    public Guid QuestionId { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string QuestionText { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

public class AnswerOption
{
    public Guid OptionId { get; set; }
    public Guid QuestionId { get; set; }
    public string OptionKey { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsCorrect { get; set; }
    public List<AnswerOptionTranslation> Translations { get; set; } = new();

    public string GetText(string lang = "en") =>
        Translations.FirstOrDefault(t => t.LanguageCode == lang)?.OptionText
        ?? Translations.FirstOrDefault(t => t.LanguageCode == "en")?.OptionText
        ?? string.Empty;
}

public class AnswerOptionTranslation
{
    public Guid OptionId { get; set; }
    public string LanguageCode { get; set; } = "en";
    public string OptionText { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
}

public class QuizAttempt
{
    public Guid AttemptId { get; set; }
    public Guid UserId { get; set; }
    public Guid QuizId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = "InProgress"; // InProgress | Passed | Failed
    public bool IsTestData { get; set; } = false;
    public decimal? ScorePercent { get; set; }
    public int? CorrectCount { get; set; }
    public int? TotalCount { get; set; }
    public int? CurrentQuestionPosition { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<AttemptAnswer> Answers { get; set; } = new();
}

public class AttemptAnswer
{
    public Guid AttemptAnswerId { get; set; }
    public Guid AttemptId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid OptionId { get; set; }
    public DateTimeOffset SelectedAt { get; set; }
}

public class CompletionRecord
{
    public Guid CompletionId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public DateTimeOffset? EmailNotificationSentAt { get; set; }
    public DateTimeOffset? ExportedAt { get; set; }
    public string SnapshotFirstName { get; set; } = string.Empty;
    public string SnapshotLastName { get; set; } = string.Empty;
    public string SnapshotEmail { get; set; } = string.Empty;
    public string SnapshotEmployeeId { get; set; } = string.Empty;
    public string SnapshotCompany { get; set; } = string.Empty;
    public string SnapshotPortfolio { get; set; } = string.Empty;
}

public class Organization
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class Portfolio
{
    public int PortfolioId { get; set; }
    public string PortfolioName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public class VbuMasterListRecord
{
    public int VbuMasterRecordId { get; set; }
    public int? PersonId { get; set; }
    public string LeaderName { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public int OrganizationId { get; set; }
    public int ReportToId { get; set; }
}

public class OrgAdminAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public int OrganizationId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
}

public class PortfolioAdminAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public int PortfolioId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
}

public static class AccessGroupTypes
{
    public const string PortfolioGroup = "PortfolioGroup";
    public const string PortfolioSubGroup = "PortfolioSubGroup";
    public const string OrgGroup = "OrgGroup";
}

public class AccessGroup
{
    public Guid AccessGroupId { get; set; }
    public string GroupTypeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PortfolioId { get; set; }
    public int? OrganizationId { get; set; }
    public int? ParentPortfolioId { get; set; }
    public bool IsSystemManaged { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class UserAccessGroupAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid UserId { get; set; }
    public Guid AccessGroupId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid AssignedByUserId { get; set; }
}
