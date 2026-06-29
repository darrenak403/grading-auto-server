using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Messaging;
using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using MassTransit;
using Moq;

namespace GradingSystem.Tests.Application.Services;

public class SubmissionServiceTests
{
    private static (SubmissionService svc, FakeUnitOfWork uow, Mock<IPublishEndpoint> publish) CreateSut()
    {
        var uow = new FakeUnitOfWork();
        var publish = new Mock<IPublishEndpoint>();
        var svc = new SubmissionService(uow, publish.Object);
        return (svc, uow, publish);
    }

    private static Submission MakeSubmission(bool hasArtifact = true) => new()
    {
        Id = Guid.NewGuid(),
        AssignmentId = Guid.NewGuid(),
        StudentCode = "se181951",
        ArtifactZipPath = hasArtifact ? "/storage/submissions/x/artifact.zip" : string.Empty,
        HasArtifact = hasArtifact,
        GradingRound = "Round 1",
        Status = SubmissionStatus.Done,
    };

    [Fact]
    public async Task RegradeAsync_LatestJobFailed_CreatesNewPendingJobAndPublishes()
    {
        var (svc, uow, publish) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);

        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            GradingRound = submission.GradingRound,
            Status = JobStatus.Failed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        });

        var result = await svc.RegradeAsync(submission.Id);

        Assert.Equal(JobStatus.Pending, result.Status);
        Assert.Equal(submission.Id, result.SubmissionId);
        Assert.Equal(SubmissionStatus.Grading, submission.Status);
        publish.Verify(p => p.Publish(It.IsAny<GradeJobMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegradeAsync_NoPriorJobs_ThrowsBadRequest()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.RegradeAsync(submission.Id));
        Assert.Contains("failed grading attempt", ex.Message);
    }

    [Fact]
    public async Task RegradeAsync_LatestJobDone_ThrowsBadRequest()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Done,
            CreatedAt = DateTime.UtcNow,
        });

        await Assert.ThrowsAsync<BadRequestException>(() => svc.RegradeAsync(submission.Id));
    }

    [Fact]
    public async Task RegradeAsync_LatestJobPending_ThrowsBadRequest_ActiveJobInFlight()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.RegradeAsync(submission.Id));
        Assert.Contains("already being graded", ex.Message);
    }

    [Fact]
    public async Task RegradeAsync_LatestJobRunning_ThrowsBadRequest_ActiveJobInFlight()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Running,
            CreatedAt = DateTime.UtcNow,
        });

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.RegradeAsync(submission.Id));
        Assert.Contains("already being graded", ex.Message);
    }

    [Fact]
    public async Task RegradeAsync_PicksMostRecentJobByCreatedAt_NotInsertionOrder()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);

        // Oldest job (Done) inserted last, newest (Failed) inserted first --
        // LatestJobStatus must order by CreatedAt, not list order.
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
        });
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Done,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        });

        var result = await svc.RegradeAsync(submission.Id);
        Assert.Equal(JobStatus.Pending, result.Status);
    }

    [Fact]
    public async Task RegradeAsync_NoArtifact_ThrowsBadRequest()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission(hasArtifact: false);
        uow.SubmissionsRepo.Items.Add(submission);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => svc.RegradeAsync(submission.Id));
        Assert.Contains("no artifact", ex.Message);
    }

    [Fact]
    public async Task RegradeAsync_SubmissionNotFound_ThrowsNotFound()
    {
        var (svc, _, _) = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.RegradeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RegradeAsync_PurgesStaleQuestionResultsBeforeRequeueing()
    {
        var (svc, uow, _) = CreateSut();
        var submission = MakeSubmission();
        uow.SubmissionsRepo.Items.Add(submission);
        uow.GradingJobsRepo.Items.Add(new GradingJob
        {
            SubmissionId = submission.Id,
            Status = JobStatus.Failed,
            CreatedAt = DateTime.UtcNow,
        });
        uow.QuestionResultsRepo.Items.Add(new QuestionResult
        {
            SubmissionId = submission.Id,
            QuestionId = Guid.NewGuid(),
            Score = 5,
            MaxScore = 10,
        });

        await svc.RegradeAsync(submission.Id);

        Assert.DoesNotContain(uow.QuestionResultsRepo.Items, r => r.SubmissionId == submission.Id);
    }

    [Fact]
    public async Task GetByAssignmentIdAsync_FiltersByGradingRound_NoBleedAcrossRounds()
    {
        var (svc, uow, _) = CreateSut();
        var assignmentId = Guid.NewGuid();

        var sub1 = new Submission
        {
            Id = Guid.NewGuid(), AssignmentId = assignmentId, StudentCode = "se1",
            GradingRound = "Round 1", ArtifactZipPath = "x", Status = SubmissionStatus.Done,
        };
        var sub2 = new Submission
        {
            Id = Guid.NewGuid(), AssignmentId = assignmentId, StudentCode = "se1",
            GradingRound = "Round 2", ArtifactZipPath = "x", Status = SubmissionStatus.Done,
        };
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Title = "A", Code = "A1" });
        uow.SubmissionsRepo.Items.Add(sub1);
        uow.SubmissionsRepo.Items.Add(sub2);

        var round1Results = await svc.GetByAssignmentIdAsync(assignmentId, null, "Round 1");
        var round2Results = await svc.GetByAssignmentIdAsync(assignmentId, null, "Round 2");
        var allResults = await svc.GetByAssignmentIdAsync(assignmentId, null, null);

        Assert.Single(round1Results);
        Assert.Equal("Round 1", round1Results[0].GradingRound);
        Assert.Single(round2Results);
        Assert.Equal("Round 2", round2Results[0].GradingRound);
        Assert.Equal(2, allResults.Count);
    }
}
