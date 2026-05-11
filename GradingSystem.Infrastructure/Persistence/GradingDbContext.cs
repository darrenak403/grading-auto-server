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

        // ExportJob — AssignmentId and ExamSessionId are mutually exclusive (one is set)
        b.Entity<ExportJob>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>();
            e.HasOne(x => x.Assignment).WithMany().HasForeignKey(x => x.AssignmentId).IsRequired(false);
            e.HasOne(x => x.ExamSession).WithMany().HasForeignKey(x => x.ExamSessionId).IsRequired(false);
        });

        // ReviewNote: one-to-one with Submission
        b.Entity<ReviewNote>(e =>
        {
            e.HasIndex(x => x.SubmissionId).IsUnique();
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
