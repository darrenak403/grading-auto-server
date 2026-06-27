using System.IO.Compression;
using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace GradingSystem.Tests.Application.Services;

public class BulkUploadServiceTests : IDisposable
{
    private readonly string _basePath;

    public BulkUploadServiceTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "bulk_upload_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_basePath, recursive: true); } catch { /* best effort */ }
    }

    private (BulkUploadService svc, FakeUnitOfWork uow) CreateSut()
    {
        var uow = new FakeUnitOfWork();

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c["Storage:BasePath"]).Returns(_basePath);

        var logger = new Mock<ILogger<BulkUploadService>>();
        var svc = new BulkUploadService(uow, configMock.Object, logger.Object);
        return (svc, uow);
    }

    /// <summary>An empty zip with no student folders: parses with zero participants/created/missing.</summary>
    private static MemoryStream EmptyZip()
    {
        var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true)) { /* no entries */ }
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task ParseAndCreateForLatestRoundAsync_NoExistingRounds_TargetsLanOne()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        using var zip = EmptyZip();
        await svc.ParseAndCreateForLatestRoundAsync(assignmentId, zip);

        // No participants, so no submissions get created; verifying via a participant proves targeting.
        var participantId = Guid.NewGuid();
        uow.ParticipantsRepo.Items.Add(new Participant
        {
            Id = participantId, ExamSessionId = Guid.NewGuid(), AssignmentId = assignmentId,
            Username = "stud1", StudentCode = "se1",
        });

        zip.Position = 0;
        await svc.ParseAndCreateForLatestRoundAsync(assignmentId, zip);

        var submission = uow.SubmissionsRepo.Items.Single(s => s.ParticipantId == participantId);
        Assert.Equal("Lần 1", submission.GradingRound);
    }

    [Fact]
    public async Task ParseAndCreateForLatestRoundAsync_ExistingRounds_TargetsHighestExistingRound()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var participantId = Guid.NewGuid();
        uow.ParticipantsRepo.Items.Add(new Participant
        {
            Id = participantId, ExamSessionId = Guid.NewGuid(), AssignmentId = assignmentId,
            Username = "stud1", StudentCode = "se1",
        });

        // Seed existing rounds "Lần 1" and "Lần 2" so latest round should be "Lần 2".
        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, ParticipantId = Guid.NewGuid(), StudentCode = "other",
            GradingRound = "Lần 1", ArtifactZipPath = "x",
        });
        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, ParticipantId = Guid.NewGuid(), StudentCode = "other2",
            GradingRound = "Lần 2", ArtifactZipPath = "x",
        });

        using var zip = EmptyZip();
        await svc.ParseAndCreateForLatestRoundAsync(assignmentId, zip);

        var submission = uow.SubmissionsRepo.Items.Single(s => s.ParticipantId == participantId);
        Assert.Equal("Lần 2", submission.GradingRound);
    }

    [Fact]
    public async Task CreateNewRoundAsync_NoExistingRounds_CreatesLanOne()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var participantId = Guid.NewGuid();
        uow.ParticipantsRepo.Items.Add(new Participant
        {
            Id = participantId, ExamSessionId = Guid.NewGuid(), AssignmentId = assignmentId,
            Username = "stud1", StudentCode = "se1",
        });

        using var zip = EmptyZip();
        await svc.CreateNewRoundAsync(assignmentId, zip);

        var submission = uow.SubmissionsRepo.Items.Single(s => s.ParticipantId == participantId);
        Assert.Equal("Lần 1", submission.GradingRound);
    }

    [Fact]
    public async Task CreateNewRoundAsync_ExistingRounds_CreatesNextSequentialRound()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var participantId = Guid.NewGuid();
        uow.ParticipantsRepo.Items.Add(new Participant
        {
            Id = participantId, ExamSessionId = Guid.NewGuid(), AssignmentId = assignmentId,
            Username = "stud1", StudentCode = "se1",
        });

        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, ParticipantId = Guid.NewGuid(), StudentCode = "other",
            GradingRound = "Lần 1", ArtifactZipPath = "x",
        });
        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, ParticipantId = Guid.NewGuid(), StudentCode = "other2",
            GradingRound = "Lần 3", ArtifactZipPath = "x",
        });

        using var zip = EmptyZip();
        await svc.CreateNewRoundAsync(assignmentId, zip);

        var submission = uow.SubmissionsRepo.Items.Single(s => s.ParticipantId == participantId);
        Assert.Equal("Lần 4", submission.GradingRound);
    }

    [Fact]
    public async Task CreateNewRoundAsync_AssignmentNotFound_ThrowsNotFound()
    {
        var (svc, _) = CreateSut();
        using var zip = EmptyZip();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.CreateNewRoundAsync(Guid.NewGuid(), zip));
    }

    [Fact]
    public async Task ParseAndCreateAsync_AssignmentNotFound_ThrowsNotFound()
    {
        var (svc, _) = CreateSut();
        using var zip = EmptyZip();
        await Assert.ThrowsAsync<NotFoundException>(
            () => svc.ParseAndCreateAsync(Guid.NewGuid(), "Lần 1", zip));
    }

    [Fact]
    public async Task ParseAndCreateAsync_NoMatchingFolders_CreatesZeroScorePlaceholderForMissingParticipant()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var participantId = Guid.NewGuid();
        uow.ParticipantsRepo.Items.Add(new Participant
        {
            Id = participantId, ExamSessionId = Guid.NewGuid(), AssignmentId = assignmentId,
            Username = "missingstudent", StudentCode = "se404",
        });

        using var zip = EmptyZip();
        var result = await svc.ParseAndCreateAsync(assignmentId, "Lần 1", zip);

        Assert.Equal(0, result.Created);
        Assert.Equal(1, result.Missing);

        var submission = uow.SubmissionsRepo.Items.Single(s => s.ParticipantId == participantId);
        Assert.False(submission.HasArtifact);
        Assert.Equal(SubmissionStatus.Done, submission.Status);
        Assert.Equal("Lần 1", submission.GradingRound);
    }
}
