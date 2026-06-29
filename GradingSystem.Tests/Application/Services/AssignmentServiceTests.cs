using GradingSystem.Application.Services;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Moq;

namespace GradingSystem.Tests.Application.Services;

public class AssignmentServiceTests
{
    private static (AssignmentService svc, FakeUnitOfWork uow) CreateSut()
    {
        var uow = new FakeUnitOfWork();
        var configMock = new Mock<IConfiguration>();
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var svc = new AssignmentService(uow, configMock.Object, publishEndpoint.Object);
        return (svc, uow);
    }

    private static Submission MakeSubmission(Guid assignmentId, string round, SubmissionStatus status = SubmissionStatus.Pending) => new()
    {
        Id = Guid.NewGuid(),
        AssignmentId = assignmentId,
        StudentCode = "se1",
        GradingRound = round,
        HasArtifact = true,
        Status = status,
    };

    [Fact]
    public async Task TriggerGradeAsync_NoRoundSpecified_TargetsLatestRound()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var round1 = MakeSubmission(assignmentId, "Round 1");
        var round2 = MakeSubmission(assignmentId, "Round 2");
        uow.SubmissionsRepo.Items.Add(round1);
        uow.SubmissionsRepo.Items.Add(round2);

        var enqueued = await svc.TriggerGradeAsync(assignmentId);

        Assert.Equal(1, enqueued);
        Assert.Equal(SubmissionStatus.Grading, round2.Status);
        Assert.Equal(SubmissionStatus.Pending, round1.Status);
    }

    [Fact]
    public async Task TriggerGradeAsync_RoundSpecified_TargetsThatRoundEvenIfNotLatest()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        var round1 = MakeSubmission(assignmentId, "Round 1");
        var round2 = MakeSubmission(assignmentId, "Round 2");
        uow.SubmissionsRepo.Items.Add(round1);
        uow.SubmissionsRepo.Items.Add(round2);

        var enqueued = await svc.TriggerGradeAsync(assignmentId, "Round 1");

        Assert.Equal(1, enqueued);
        Assert.Equal(SubmissionStatus.Grading, round1.Status);
        Assert.Equal(SubmissionStatus.Pending, round2.Status);
    }

    [Fact]
    public async Task TriggerGradeAsync_RoundSpecifiedWithNoPendingSubmissions_EnqueuesNothing()
    {
        var (svc, uow) = CreateSut();
        var assignmentId = Guid.NewGuid();
        uow.AssignmentsRepo.Items.Add(new Assignment { Id = assignmentId, Code = "A1", Title = "A" });

        uow.SubmissionsRepo.Items.Add(MakeSubmission(assignmentId, "Round 1", SubmissionStatus.Done));

        var enqueued = await svc.TriggerGradeAsync(assignmentId, "Round 1");

        Assert.Equal(0, enqueued);
    }
}
