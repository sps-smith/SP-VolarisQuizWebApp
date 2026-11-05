using Microsoft.EntityFrameworkCore;
using Volaris101.Domain.Entities;

namespace Volaris101.Infrastructure.Data;

public class Volaris101DbContext : DbContext
{
    public Volaris101DbContext(DbContextOptions<Volaris101DbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<VbuMasterListRecord> VbuMasterList => Set<VbuMasterListRecord>();
    public DbSet<OrgAdminAssignment> OrgAdminAssignments => Set<OrgAdminAssignment>();
    public DbSet<PortfolioAdminAssignment> PortfolioAdminAssignments => Set<PortfolioAdminAssignment>();
    public DbSet<AccessGroup> AccessGroups => Set<AccessGroup>();
    public DbSet<UserAccessGroupAssignment> UserAccessGroupAssignments => Set<UserAccessGroupAssignment>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizTranslation> QuizTranslations => Set<QuizTranslation>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionTranslation> QuestionTranslations => Set<QuestionTranslation>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<AnswerOptionTranslation> AnswerOptionTranslations => Set<AnswerOptionTranslation>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();
    public DbSet<ReportingAttemptSummaryRow> ReportingAttemptSummaryRows => Set<ReportingAttemptSummaryRow>();
    public DbSet<AttemptAnswer> AttemptAnswers => Set<AttemptAnswer>();
    public DbSet<CompletionRecord> CompletionRecords => Set<CompletionRecord>();
    public DbSet<AuditLogEntry> AdminAuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // ── Users ──────────────────────────────────────────────────────────────
        model.Entity<User>(e =>
        {
            e.ToTable("Users", tb =>
            {
                tb.HasTrigger("TR_Users_RefreshLanguageSummaryCache");
            });
            e.HasKey(u => u.UserId);
            e.Property(u => u.UserId).HasColumnName("UserID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(u => u.Login).HasMaxLength(256).IsRequired();
            e.Property(u => u.IdpSource).HasMaxLength(50).HasDefaultValue("Okta");
            e.Property(u => u.IdpSubjectIdentifier).HasMaxLength(256).IsRequired();
            e.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
            e.Property(u => u.LastName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.EmployeeId).HasMaxLength(100).IsRequired();
            e.Property(u => u.Company).HasMaxLength(200).IsRequired(false);
            e.Property(u => u.Portfolio).HasMaxLength(200).IsRequired(false);
            e.Property(u => u.OrganizationId).HasColumnName("OrganizationID").HasColumnType("INT").IsRequired(false);
            e.Property(u => u.PortfolioId).HasColumnName("PortfolioID").HasColumnType("INT").IsRequired(false);
            e.Property(u => u.PreferredLanguage).HasColumnType("CHAR(2)").HasDefaultValue("en");
            e.Property(u => u.RoleCode).HasMaxLength(20).HasDefaultValue("Learner");
            e.Property(u => u.AccountCreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(u => u.Login).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasIndex(u => u.EmployeeId);
            e.HasIndex(u => u.RoleCode);
            // Ignore computed properties
            e.Ignore(u => u.FullName);
            e.Ignore(u => u.Initials);
        });

        // ── Organizations ──────────────────────────────────────────────────────
        model.Entity<Organization>(e =>
        {
            e.ToTable("Organizations");
            e.HasKey(o => o.OrganizationId);
            e.Property(o => o.OrganizationId).HasColumnName("OrganizationID").HasColumnType("INT").ValueGeneratedOnAdd();
            e.Property(o => o.OrganizationName).HasMaxLength(200).IsRequired();
            e.Property(o => o.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(o => o.OrganizationName).IsUnique();
        });

        model.Entity<Portfolio>(e =>
        {
            e.ToTable("Portfolios");
            e.HasKey(p => p.PortfolioId);
            e.Property(p => p.PortfolioId).HasColumnName("PortfolioID").HasColumnType("INT").ValueGeneratedNever();
            e.Property(p => p.PortfolioName).HasMaxLength(200).IsRequired();
            e.Property(p => p.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(p => p.PortfolioName).IsUnique();
        });

        model.Entity<VbuMasterListRecord>(e =>
        {
            e.ToTable("VBUMasterListProd");
            e.HasKey(v => v.VbuMasterRecordId);
            e.Property(v => v.VbuMasterRecordId).HasColumnName("VBUMasterRecordID").HasColumnType("INT").ValueGeneratedNever();
            e.Property(v => v.PersonId).HasColumnName("PersonID").HasColumnType("INT");
            e.Property(v => v.LeaderName).HasMaxLength(200).HasDefaultValue(string.Empty);
            e.Property(v => v.OrganizationName).HasMaxLength(200).IsRequired();
            e.Property(v => v.OrganizationId).HasColumnName("OrganizationID").HasColumnType("INT").IsRequired();
            e.Property(v => v.ReportToId).HasColumnName("ReportToID").HasColumnType("INT").IsRequired();
            e.HasIndex(v => v.OrganizationId);
            e.HasIndex(v => v.ReportToId);
        });

        model.Entity<OrgAdminAssignment>(e =>
        {
            e.ToTable("OrgAdminAssignments");
            e.HasKey(a => a.AssignmentId);
            e.Property(a => a.AssignmentId).HasColumnName("AssignmentID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(a => a.UserId).HasColumnName("UserID");
            e.Property(a => a.OrganizationId).HasColumnName("OrganizationID").HasColumnType("INT");
            e.Property(a => a.AssignedAt).HasColumnName("AssignedAt").HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(a => a.AssignedByUserId).HasColumnName("AssignedByUserID");
            e.HasIndex(a => new { a.UserId, a.OrganizationId }).IsUnique();
            e.HasIndex(a => a.OrganizationId);
        });

        model.Entity<PortfolioAdminAssignment>(e =>
        {
            e.ToTable("PortfolioAdminAssignments");
            e.HasKey(a => a.AssignmentId);
            e.Property(a => a.AssignmentId).HasColumnName("AssignmentID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(a => a.UserId).HasColumnName("UserID");
            e.Property(a => a.PortfolioId).HasColumnName("PortfolioID").HasColumnType("INT");
            e.Property(a => a.AssignedAt).HasColumnName("AssignedAt").HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(a => a.AssignedByUserId).HasColumnName("AssignedByUserID");
            e.HasIndex(a => new { a.UserId, a.PortfolioId }).IsUnique();
            e.HasIndex(a => a.PortfolioId);
        });

        // ── Quizzes ────────────────────────────────────────────────────────────
        model.Entity<AccessGroup>(e =>
        {
            e.ToTable("AccessGroups");
            e.HasKey(g => g.AccessGroupId);
            e.Property(g => g.AccessGroupId).HasColumnName("AccessGroupID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(g => g.GroupTypeCode).HasMaxLength(30).IsRequired();
            e.Property(g => g.Name).HasMaxLength(200).IsRequired();
            e.Property(g => g.Description).HasMaxLength(1000).IsRequired(false);
            e.Property(g => g.PortfolioId).HasColumnName("PortfolioID").HasColumnType("INT").IsRequired(false);
            e.Property(g => g.OrganizationId).HasColumnName("OrganizationID").HasColumnType("INT").IsRequired(false);
            e.Property(g => g.ParentPortfolioId).HasColumnName("ParentPortfolioID").HasColumnType("INT").IsRequired(false);
            e.Property(g => g.IsSystemManaged).HasDefaultValue(false);
            e.Property(g => g.IsActive).HasDefaultValue(true);
            e.Property(g => g.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(g => g.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(g => g.GroupTypeCode);
            e.HasIndex(g => g.PortfolioId);
            e.HasIndex(g => g.OrganizationId);
            e.HasIndex(g => g.ParentPortfolioId);
            e.HasIndex(g => new { g.GroupTypeCode, g.PortfolioId }).IsUnique().HasFilter("[PortfolioID] IS NOT NULL");
            e.HasIndex(g => new { g.GroupTypeCode, g.OrganizationId }).IsUnique().HasFilter("[OrganizationID] IS NOT NULL");
        });

        model.Entity<UserAccessGroupAssignment>(e =>
        {
            e.ToTable("UserAccessGroupAssignments");
            e.HasKey(a => a.AssignmentId);
            e.Property(a => a.AssignmentId).HasColumnName("AssignmentID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(a => a.UserId).HasColumnName("UserID");
            e.Property(a => a.AccessGroupId).HasColumnName("AccessGroupID");
            e.Property(a => a.AssignedAt).HasColumnName("AssignedAt").HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(a => a.AssignedByUserId).HasColumnName("AssignedByUserID");
            e.HasIndex(a => new { a.UserId, a.AccessGroupId }).IsUnique();
            e.HasIndex(a => a.AccessGroupId);
        });

        model.Entity<Quiz>(e =>
        {
            e.ToTable("Quizzes");
            e.HasKey(q => q.QuizId);
            e.Property(q => q.QuizId).HasColumnName("QuizID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(q => q.PassingScore).HasColumnType("DECIMAL(5,2)").HasDefaultValue(50m);
            e.Property(q => q.IsPublished).HasDefaultValue(false);
            e.Property(q => q.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(q => q.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(q => q.DisplayOrder).IsUnique();
            e.HasMany(q => q.Translations).WithOne().HasForeignKey(t => t.QuizId);
            e.HasMany(q => q.Questions).WithOne().HasForeignKey(q => q.QuizId);
        });

        // ── QuizTranslations ───────────────────────────────────────────────────
        model.Entity<QuizTranslation>(e =>
        {
            e.ToTable("QuizTranslations");
            e.HasKey(t => new { t.QuizId, t.LanguageCode });
            e.Property(t => t.QuizId).HasColumnName("QuizID");
            e.Property(t => t.LanguageCode).HasColumnType("CHAR(2)");
            e.Property(t => t.Title).HasMaxLength(500).IsRequired();
            e.Ignore(t => t.IsComplete);
        });

        // ── Questions ──────────────────────────────────────────────────────────
        model.Entity<Question>(e =>
        {
            e.ToTable("Questions");
            e.HasKey(q => q.QuestionId);
            e.Property(q => q.QuestionId).HasColumnName("QuestionID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(q => q.QuizId).HasColumnName("QuizID");
            e.Property(q => q.CreatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(q => q.UpdatedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasMany(q => q.Translations).WithOne().HasForeignKey(t => t.QuestionId);
            e.HasMany(q => q.Options).WithOne().HasForeignKey(o => o.QuestionId);
        });

        // ── QuestionTranslations ───────────────────────────────────────────────
        model.Entity<QuestionTranslation>(e =>
        {
            e.ToTable("QuestionTranslations");
            e.HasKey(t => new { t.QuestionId, t.LanguageCode });
            e.Property(t => t.QuestionId).HasColumnName("QuestionID");
            e.Property(t => t.LanguageCode).HasColumnType("CHAR(2)");
            e.Property(t => t.QuestionText).HasMaxLength(2000).IsRequired();
            e.Ignore(t => t.IsComplete);
        });

        // ── AnswerOptions ──────────────────────────────────────────────────────
        model.Entity<AnswerOption>(e =>
        {
            e.ToTable("AnswerOptions");
            e.HasKey(o => o.OptionId);
            e.Property(o => o.OptionId).HasColumnName("OptionID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(o => o.QuestionId).HasColumnName("QuestionID");
            e.Property(o => o.OptionKey).HasMaxLength(10).IsRequired();
            e.HasMany(o => o.Translations).WithOne().HasForeignKey(t => t.OptionId);
        });

        // ── AnswerOptionTranslations ───────────────────────────────────────────
        model.Entity<AnswerOptionTranslation>(e =>
        {
            e.ToTable("AnswerOptionTranslations");
            e.HasKey(t => new { t.OptionId, t.LanguageCode });
            e.Property(t => t.OptionId).HasColumnName("OptionID");
            e.Property(t => t.LanguageCode).HasColumnType("CHAR(2)");
            e.Property(t => t.OptionText).HasMaxLength(1000).IsRequired();
            e.Ignore(t => t.IsComplete);
        });

        // ── QuizAttempts ───────────────────────────────────────────────────────
        model.Entity<QuizAttempt>(e =>
        {
            e.HasKey(a => a.AttemptId);
            e.Property(a => a.AttemptId).HasColumnName("AttemptID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(a => a.UserId).HasColumnName("UserID");
            e.Property(a => a.QuizId).HasColumnName("QuizID");
            e.Property(a => a.IsTestData).HasDefaultValue(false);
            e.Property(a => a.ScorePercent).HasColumnType("DECIMAL(5,2)");
            e.Property(a => a.CorrectCount).HasColumnName("RawScore");
            e.Property(a => a.TotalCount).HasColumnName("TotalQuestions");
            e.Property(a => a.StartedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(a => new { a.UserId, a.QuizId });
            e.ToTable("QuizAttempts", tb =>
            {
                tb.HasTrigger("TR_QuizAttempts_RefreshReportingAttemptSummaryCache");
                tb.HasTrigger("TR_QuizAttempts_RefreshLanguageSummaryCache");
            });
            e.HasMany(a => a.Answers).WithOne().HasForeignKey(aa => aa.AttemptId);
        });

        // ── AttemptAnswers ─────────────────────────────────────────────────────
        model.Entity<ReportingAttemptSummaryRow>(e =>
        {
            e.ToTable("ReportingAttemptSummaryCache", "dbo");
            e.HasKey(r => new { r.UserId, r.QuizId, r.IsTestData });
            e.Property(r => r.UserId).HasColumnName("UserID");
            e.Property(r => r.QuizId).HasColumnName("QuizID");
            e.Property(r => r.IsTestData).HasColumnName("IsTestData");
            e.Property(r => r.HasPassed).HasColumnName("HasPassed");
            e.Property(r => r.TotalAttempts).HasColumnName("TotalAttempts");
            e.Property(r => r.BestScore).HasColumnName("BestScore");
            e.Property(r => r.LastAttemptAt).HasColumnName("LastAttemptAt");
        });

        model.Entity<AttemptAnswer>(e =>
        {
            e.ToTable("AttemptAnswers");
            e.HasKey(aa => aa.AttemptAnswerId);
            e.Property(aa => aa.AttemptAnswerId).HasColumnName("AttemptAnswerID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(aa => aa.AttemptId).HasColumnName("AttemptID");
            e.Property(aa => aa.QuestionId).HasColumnName("QuestionID");
            e.Property(aa => aa.OptionId).HasColumnName("OptionID");
            e.Property(aa => aa.SelectedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.HasIndex(aa => new { aa.AttemptId, aa.QuestionId });
        });

        // ── CompletionRecords ──────────────────────────────────────────────────
        model.Entity<CompletionRecord>(e =>
        {
            e.ToTable("CompletionRecords");
            e.HasKey(c => c.CompletionId);
            e.Property(c => c.CompletionId).HasColumnName("CompletionID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(c => c.UserId).HasColumnName("UserID");
            e.Property(c => c.CompletedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(c => c.SnapshotFirstName).HasMaxLength(100).IsRequired();
            e.Property(c => c.SnapshotLastName).HasMaxLength(100).IsRequired();
            e.Property(c => c.SnapshotEmail).HasMaxLength(256).IsRequired();
            e.Property(c => c.SnapshotEmployeeId).HasMaxLength(100).IsRequired();
            e.Property(c => c.SnapshotCompany).HasMaxLength(200).IsRequired();
            e.Property(c => c.SnapshotPortfolio).HasMaxLength(200).IsRequired();
            e.HasIndex(c => c.UserId).IsUnique();
        });

        // ── AdminAuditLog ──────────────────────────────────────────────────────
        model.Entity<AuditLogEntry>(e =>
        {
            e.ToTable("AdminAuditLog");
            e.HasKey(l => l.LogEntryId);
            e.Property(l => l.LogEntryId).HasColumnName("LogEntryID").HasDefaultValueSql("NEWSEQUENTIALID()");
            e.Property(l => l.LoggedAt).HasDefaultValueSql("SYSDATETIMEOFFSET()");
            e.Property(l => l.ActionType).HasMaxLength(100).IsRequired();
            e.Property(l => l.Outcome).HasMaxLength(50).IsRequired();
            e.Property(l => l.ActorType).HasMaxLength(20).HasDefaultValue("system");
            e.Property(l => l.DenialReason).HasMaxLength(100);
            e.Property(l => l.SourceComponent).HasMaxLength(100);
            e.Property(l => l.IpAddress).HasMaxLength(45);
            e.Property(l => l.SessionId).HasMaxLength(256);
            e.Property(l => l.ActorUserId).HasColumnName("ActorUserID");
            e.Property(l => l.TargetUserId).HasColumnName("TargetUserID");
            e.HasIndex(l => l.LoggedAt);
            e.HasIndex(l => l.ActionType);
            e.HasIndex(l => l.ActorUserId);
        });
    }
}

// Separate EF-only entity for the audit log (not in domain)
public class AuditLogEntry
{
    public Guid LogEntryId { get; set; }
    public DateTimeOffset LoggedAt { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public string? DenialReason { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorType { get; set; } = "system";
    public Guid? TargetUserId { get; set; }
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    public string? SourceComponent { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? IpAddress { get; set; }
    public string? SessionId { get; set; }
    public string? AdditionalData { get; set; }
}
