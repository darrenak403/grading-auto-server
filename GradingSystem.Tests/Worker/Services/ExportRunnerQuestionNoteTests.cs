using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Domain.Entities;
using GradingSystem.Tests.Fakes;
using GradingSystem.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;

namespace GradingSystem.Tests.Worker.Services;

public class ExportRunnerQuestionNoteTests
{
    [Fact]
    public async Task GenerateAsync_FailedApiTest_WritesEndpointAndActualStatusInQuestionNote()
    {
        var storageRoot = Path.Combine(
            Path.GetTempPath(), "grading-export-note-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);

        try
        {
            var uow = new FakeUnitOfWork();
            var assignment = new Assignment { Id = Guid.NewGuid(), Code = "7", Title = "Mã đề 7" };
            var question = new Question
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                Title = "Q1",
                Type = QuestionType.Api,
                MaxScore = 10,
                ArtifactFolderName = "1",
            };
            var testCase = new TestCase
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                Name = "Create product",
                HttpMethod = "POST",
                UrlTemplate = "/api/products",
                Score = 10,
            };
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                StudentCode = "SE180240",
                GradingRound = "Lần 1",
                Status = SubmissionStatus.Done,
            };
            var gradingJob = new GradingJob
            {
                Id = Guid.NewGuid(),
                SubmissionId = submission.Id,
                GradingRound = submission.GradingRound,
                Status = JobStatus.Done,
                CreatedAt = DateTime.UtcNow,
                FinishedAt = DateTime.UtcNow,
            };
            var detail = JsonSerializer.Serialize(new[]
            {
                new TestCaseResult
                {
                    TestCaseId = testCase.Id,
                    Pass = false,
                    AwardedScore = 0,
                    HttpMethod = "POST",
                    Url = "http://localhost:7001/api/products",
                    ActualStatus = 500,
                },
            });

            uow.AssignmentsRepo.Items.Add(assignment);
            uow.QuestionsRepo.Items.Add(question);
            uow.TestCasesRepo.Items.Add(testCase);
            uow.SubmissionsRepo.Items.Add(submission);
            uow.GradingJobsRepo.Items.Add(gradingJob);
            uow.QuestionResultsRepo.Items.Add(new QuestionResult
            {
                SubmissionId = submission.Id,
                GradingJobId = gradingJob.Id,
                QuestionId = question.Id,
                Score = 0,
                MaxScore = question.MaxScore,
                Detail = detail,
            });

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:BasePath"] = storageRoot,
                })
                .Build();
            var runner = new ExportRunner(config, NullLogger<ExportRunner>.Instance);
            var path = await runner.GenerateAsync(new ExportJob
            {
                AssignmentId = assignment.Id,
                GradingRound = submission.GradingRound,
            }, uow, CancellationToken.None);

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(new FileInfo(path));
            var sheet = package.Workbook.Worksheets[assignment.Code];

            Assert.NotNull(sheet);
            Assert.Equal(
                "Sai: Create product — POST /api/products — HTTP 500",
                sheet.Cells[2, 6].Text);
        }
        finally
        {
            if (Directory.Exists(storageRoot))
                Directory.Delete(storageRoot, recursive: true);
        }
    }
}
