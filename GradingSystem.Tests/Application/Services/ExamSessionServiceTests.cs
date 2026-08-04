using GradingSystem.Application.Exceptions;
using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using MassTransit;
using Moq;

namespace GradingSystem.Tests.Application.Services;

public class ExamSessionServiceTests
{
    private static (ExamSessionService svc, FakeUnitOfWork uow) CreateSut()
    {
        var uow = new FakeUnitOfWork();
        var publish = new Mock<IPublishEndpoint>();
        var svc = new ExamSessionService(uow, publish.Object);
        return (svc, uow);
    }

    private static (Guid sessionId, Guid assignmentId) SeedSessionWithAssignment(FakeUnitOfWork uow)
    {
        var sessionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        uow.ExamSessionsRepo.Items.Add(new ExamSession { Id = sessionId, Title = "Session 1" });
        uow.AssignmentsRepo.Items.Add(new Assignment
        {
            Id = assignmentId, Code = "A1", Title = "Assignment 1", ExamSessionId = sessionId,
        });
        return (sessionId, assignmentId);
    }

    [Fact]
    public async Task GetSessionResultsAsync_MultipleRounds_NoRoundSpecified_ThrowsBadRequest()
    {
        var (svc, uow) = CreateSut();
        var (sessionId, assignmentId) = SeedSessionWithAssignment(uow);

        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se1", GradingRound = "Round 1", ArtifactZipPath = "x",
        });
        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se1", GradingRound = "Round 2", ArtifactZipPath = "x",
        });

        await Assert.ThrowsAsync<BadRequestException>(() => svc.GetSessionResultsAsync(sessionId, null));
    }

    [Fact]
    public async Task GetSessionResultsAsync_SingleRound_NoRoundSpecified_DoesNotThrow()
    {
        var (svc, uow) = CreateSut();
        var (sessionId, assignmentId) = SeedSessionWithAssignment(uow);

        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se1", GradingRound = "Round 1", ArtifactZipPath = "x",
        });

        var results = await svc.GetSessionResultsAsync(sessionId, null);
        Assert.Single(results);
        Assert.Equal("Round 1", results[0].GradingRound);
    }

    [Fact]
    public async Task GetSessionResultsAsync_RoundSpecified_OnlyReturnsThatRound_NoBleed()
    {
        var (svc, uow) = CreateSut();
        var (sessionId, assignmentId) = SeedSessionWithAssignment(uow);

        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se1", GradingRound = "Round 1", ArtifactZipPath = "x",
        });
        uow.SubmissionsRepo.Items.Add(new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se2", GradingRound = "Round 2", ArtifactZipPath = "x",
        });

        var resultsRound1 = await svc.GetSessionResultsAsync(sessionId, "Round 1");
        var resultsRound2 = await svc.GetSessionResultsAsync(sessionId, "Round 2");

        Assert.Single(resultsRound1);
        Assert.Equal("se1", resultsRound1[0].StudentCode);
        Assert.Equal("Round 1", resultsRound1[0].GradingRound);

        Assert.Single(resultsRound2);
        Assert.Equal("se2", resultsRound2[0].StudentCode);
        Assert.Equal("Round 2", resultsRound2[0].GradingRound);
    }

    [Fact]
    public async Task GetSessionResultsAsync_SessionNotFound_ThrowsNotFound()
    {
        var (svc, _) = CreateSut();
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetSessionResultsAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task GetSessionResultsAsync_NoSubmissionsAtAll_ReturnsEmptyWithoutThrowing()
    {
        var (svc, uow) = CreateSut();
        var (sessionId, _) = SeedSessionWithAssignment(uow);

        var results = await svc.GetSessionResultsAsync(sessionId, null);
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetSessionResultsAsync_UsesLatestDoneJobResultsOnly_NotStaleJobResults()
    {
        var (svc, uow) = CreateSut();
        var (sessionId, assignmentId) = SeedSessionWithAssignment(uow);

        var question = new Question { AssignmentId = assignmentId, Title = "Q1", MaxScore = 10 };
        uow.QuestionsRepo.Items.Add(question);

        var submission = new Submission
        {
            AssignmentId = assignmentId, StudentCode = "se1", GradingRound = "Round 1", ArtifactZipPath = "x",
        };
        uow.SubmissionsRepo.Items.Add(submission);

        var oldJob = new GradingJob
        {
            SubmissionId = submission.Id, Status = JobStatus.Done,
            FinishedAt = DateTime.UtcNow.AddHours(-2),
        };
        var newJob = new GradingJob
        {
            SubmissionId = submission.Id, Status = JobStatus.Done,
            FinishedAt = DateTime.UtcNow,
        };
        uow.GradingJobsRepo.Items.Add(oldJob);
        uow.GradingJobsRepo.Items.Add(newJob);

        uow.QuestionResultsRepo.Items.Add(new QuestionResult
        {
            SubmissionId = submission.Id, QuestionId = question.Id, GradingJobId = oldJob.Id, Score = 3, MaxScore = 10,
        });
        uow.QuestionResultsRepo.Items.Add(new QuestionResult
        {
            SubmissionId = submission.Id, QuestionId = question.Id, GradingJobId = newJob.Id, Score = 9, MaxScore = 10,
        });

        var results = await svc.GetSessionResultsAsync(sessionId, null);

        Assert.Single(results);
        Assert.Equal(9, results[0].TotalScore);
    }
}
