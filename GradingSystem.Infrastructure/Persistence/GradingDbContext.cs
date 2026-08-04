using GradingSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradingSystem.Infrastructure.Persistence;

public class GradingDbContext(DbContextOptions<GradingDbContext> options) : DbContext(options)
{
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<TestCase> TestCases => Set<TestCase>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<GradingJob> GradingJobs => Set<GradingJob>();
    public DbSet<QuestionResult> QuestionResults => Set<QuestionResult>();
    public DbSet<ReviewNote> ReviewNotes => Set<ReviewNote>();
    public DbSet<ExportJob> ExportJobs => Set<ExportJob>();

    // Lab grading
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<LabAssignment> LabAssignments => Set<LabAssignment>();
    public DbSet<LabTestCase> LabTestCases => Set<LabTestCase>();
    public DbSet<LabSubmission> LabSubmissions => Set<LabSubmission>();
    public DbSet<LabGradingJob> LabGradingJobs => Set<LabGradingJob>();
    public DbSet<LabTestCaseResult> LabTestCaseResults => Set<LabTestCaseResult>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ExamSession
        b.Entity<ExamSession>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        });

        // Participant
        b.Entity<Participant>(e =>
        {
            e.Property(x => x.Username).HasMaxLength(100).IsRequired();
            e.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.ExamSessionId, x.Username }).IsUnique();
        });

        // Assignment: Code unique within ExamSession (null sessions also unique per code)
        b.Entity<Assignment>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.ExamSessionId, x.Code }).IsUnique()
                .HasFilter("\"ExamSessionId\" IS NOT NULL");
            e.HasIndex(x => x.Code).IsUnique()
                .HasFilter("\"ExamSessionId\" IS NULL");
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.GivenApiBaseUrl).HasMaxLength(500);
        });

        // Question
        b.Entity<Question>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<string>();
            e.Property(x => x.ArtifactFolderName).HasMaxLength(100).IsRequired();
        });

        // TestCase
        b.Entity<TestCase>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            e.Property(x => x.UrlTemplate).HasMaxLength(500).IsRequired();
            e.Property(x => x.Score).HasPrecision(10, 2);
        });

        // Submission
        b.Entity<Submission>(e =>
        {
            e.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.GradingRound).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.ParticipantId, x.GradingRound }).IsUnique()
                .HasFilter("\"ParticipantId\" IS NOT NULL");
        });

        // GradingJob
        b.Entity<GradingJob>(e =>
        {
            e.Property(x => x.GradingRound).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
        });

        // QuestionResult: unique per (GradingJobId, QuestionId) when GradingJobId set
        b.Entity<QuestionResult>(e =>
        {
            e.HasIndex(x => new { x.GradingJobId, x.QuestionId }).IsUnique()
                .HasFilter("\"GradingJobId\" IS NOT NULL");
            e.Property(x => x.Score).HasPrecision(10, 2);
            e.Property(x => x.AdjustedScore).HasPrecision(10, 2);
        });

        // ExportJob — exactly one of AssignmentId, ExamSessionId, LabAssignmentId is set
        b.Entity<ExportJob>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).IsRequired(false);
            e.HasOne(x => x.ExamSession).WithMany().HasForeignKey(x => x.ExamSessionId).IsRequired(false);
            e.HasOne(x => x.LabAssignment).WithMany().HasForeignKey(x => x.LabAssignmentId).IsRequired(false);
        });

        // ReviewNote: one-to-one with Submission
        b.Entity<ReviewNote>(e =>
        {
            e.HasIndex(x => x.SubmissionId).IsUnique();
        });

        // Semester
        b.Entity<Semester>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
        });

        // LabAssignment
        b.Entity<LabAssignment>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Semester).WithMany(s => s.LabAssignments)
                .HasForeignKey(x => x.SemesterId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        });

        // LabTestCase
        b.Entity<LabTestCase>(e =>
        {
            e.Property(x => x.HttpMethod).HasMaxLength(10).IsRequired();
            e.Property(x => x.UrlTemplate).HasMaxLength(500).IsRequired();
            e.Property(x => x.SaveTokenFrom).HasMaxLength(200);
            e.Property(x => x.HeadersJson).HasColumnType("text");
            e.Property(x => x.Score).HasPrecision(5, 2);
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.MatchMode).HasConversion<string>();
            e.HasOne(x => x.LabAssignment).WithMany(a => a.TestCases)
                .HasForeignKey(x => x.LabAssignmentId).OnDelete(DeleteBehavior.Cascade);
        });

        // LabSubmission
        b.Entity<LabSubmission>(e =>
        {
            e.Property(x => x.StudentCode).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.HasIndex(x => new { x.LabAssignmentId, x.StudentCode }).IsUnique();
            e.HasOne(x => x.LabAssignment).WithMany(a => a.Submissions)
                .HasForeignKey(x => x.LabAssignmentId).OnDelete(DeleteBehavior.Cascade);
        });

        // LabGradingJob
        b.Entity<LabGradingJob>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.LabSubmission).WithMany(s => s.GradingJobs)
                .HasForeignKey(x => x.LabSubmissionId).OnDelete(DeleteBehavior.Cascade);
        });

        // LabTestCaseResult
        b.Entity<LabTestCaseResult>(e =>
        {
            e.Property(x => x.AwardedScore).HasPrecision(5, 2);
            e.Property(x => x.ManualOverrideScore).HasPrecision(5, 2);
            e.HasOne(x => x.LabGradingJob).WithMany(j => j.Results)
                .HasForeignKey(x => x.LabGradingJobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LabTestCase).WithMany()
                .HasForeignKey(x => x.LabTestCaseId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(ct);
    }
}
