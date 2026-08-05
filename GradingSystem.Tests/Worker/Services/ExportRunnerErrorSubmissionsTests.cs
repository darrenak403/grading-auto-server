using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using GradingSystem.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;

namespace GradingSystem.Tests.Worker.Services;

public class ExportRunnerErrorSubmissionsTests
{
    [Fact]
    public async Task GenerateAsync_LatestAttemptFailed_WritesErrorSubmissionSheet()
    {
        var storageRoot = CreateStorageRoot();
        try
        {
            var uow = new FakeUnitOfWork();
            var assignment = MakeAssignment("2");
            var submission = MakeSubmission(assignment.Id, "SE180240", "Lần 3");
            var failedJob = MakeJob(
                submission.Id, submission.GradingRound, JobStatus.Failed, DateTime.UtcNow, "Ứng dụng bị crash.");
            uow.AssignmentsRepo.Items.Add(assignment);
            uow.SubmissionsRepo.Items.Add(submission);
            uow.GradingJobsRepo.Items.Add(failedJob);

            var path = await CreateRunner(storageRoot).GenerateAsync(new ExportJob
            {
                AssignmentId = assignment.Id,
                GradingRound = submission.GradingRound,
            }, uow, CancellationToken.None);

            using var package = OpenPackage(path);
            var sheet = package.Workbook.Worksheets["Error_Submissions"];

            Assert.NotNull(sheet);
            Assert.Equal("MSSV", sheet.Cells[1, 1].Text);
            Assert.Equal("Mã đề", sheet.Cells[1, 2].Text);
            Assert.Equal("Lần chấm", sheet.Cells[1, 3].Text);
            Assert.Equal("Lý do lỗi", sheet.Cells[1, 4].Text);
            Assert.Equal(submission.StudentCode, sheet.Cells[2, 1].Text);
            Assert.Equal(assignment.Code, sheet.Cells[2, 2].Text);
            Assert.Equal(submission.GradingRound, sheet.Cells[2, 3].Text);
            Assert.Equal(failedJob.ErrorMessage, sheet.Cells[2, 4].Text);
        }
        finally
        {
            DeleteStorageRoot(storageRoot);
        }
    }

    [Fact]
    public async Task GenerateAsync_ExcludesRecoveredSubmissionAndOtherRound()
    {
        var storageRoot = CreateStorageRoot();
        try
        {
            var uow = new FakeUnitOfWork();
            var assignment = MakeAssignment("4");
            var recovered = MakeSubmission(assignment.Id, "SE180001", "Lần 2");
            var otherRound = MakeSubmission(assignment.Id, "SE180002", "Lần 1");
            var now = DateTime.UtcNow;
            uow.AssignmentsRepo.Items.Add(assignment);
            uow.SubmissionsRepo.Items.AddRange([recovered, otherRound]);
            uow.GradingJobsRepo.Items.AddRange([
                MakeJob(recovered.Id, "Lần 2", JobStatus.Failed, now.AddMinutes(-3), "Lỗi cũ"),
                MakeJob(recovered.Id, "Lần 2", JobStatus.Done, now.AddMinutes(-2)),
                MakeJob(recovered.Id, "Lần 1", JobStatus.Failed, now.AddMinutes(-1), "Lỗi khác round"),
                MakeJob(otherRound.Id, "Lần 1", JobStatus.Failed, now, "Lỗi round khác"),
            ]);

            var path = await CreateRunner(storageRoot).GenerateAsync(new ExportJob
            {
                AssignmentId = assignment.Id,
                GradingRound = "Lần 2",
            }, uow, CancellationToken.None);

            using var package = OpenPackage(path);
            var sheet = package.Workbook.Worksheets["Error_Submissions"];

            Assert.NotNull(sheet);
            Assert.Equal(1, sheet.Dimension.End.Row);
        }
        finally
        {
            DeleteStorageRoot(storageRoot);
        }
    }

    private static ExportRunner CreateRunner(string storageRoot)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:BasePath"] = storageRoot,
            })
            .Build();
        return new ExportRunner(config, NullLogger<ExportRunner>.Instance);
    }

    private static ExcelPackage OpenPackage(string path)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        return new ExcelPackage(new FileInfo(path));
    }

    private static Assignment MakeAssignment(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Title = $"Mã đề {code}",
    };

    private static Submission MakeSubmission(Guid assignmentId, string studentCode, string gradingRound) => new()
    {
        Id = Guid.NewGuid(),
        AssignmentId = assignmentId,
        StudentCode = studentCode,
        GradingRound = gradingRound,
        Status = SubmissionStatus.Error,
    };

    private static GradingJob MakeJob(
        Guid submissionId, string gradingRound, JobStatus status, DateTime createdAt,
        string? errorMessage = null) => new()
    {
        Id = Guid.NewGuid(),
        SubmissionId = submissionId,
        GradingRound = gradingRound,
        Status = status,
        CreatedAt = createdAt,
        FinishedAt = createdAt,
        ErrorMessage = errorMessage,
    };

    private static string CreateStorageRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "grading-export-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteStorageRoot(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
