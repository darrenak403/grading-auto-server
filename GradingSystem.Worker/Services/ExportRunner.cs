using System.Text.Json;
using GradingSystem.Application.Common;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using OfficeOpenXml;

namespace GradingSystem.Worker.Services;

public class ExportRunner(
    IConfiguration config,
    ILogger<ExportRunner> logger)
{
    public async Task<string> GenerateAsync(ExportJob job, IUnitOfWork uow, CancellationToken ct)
    {
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        using var pkg = new ExcelPackage();

        if (job.ExamSessionId.HasValue)
            await BuildSessionSheetsAsync(pkg, job, uow, ct);
        else if (job.AssignmentId.HasValue)
            await BuildAssignmentSheetAsync(pkg, job.AssignmentId.Value, job.GradingRound, uow, ct);
        else
            throw new InvalidOperationException("ExportJob has neither AssignmentId nor ExamSessionId.");

        var dir  = Path.Combine(config["Storage:BasePath"]!, "exports");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{job.Id}.xlsx");
        await pkg.SaveAsAsync(new FileInfo(path), ct);

        logger.LogInformation("Export {JobId} saved to {Path}", job.Id, path);
        return path;
    }

    // ── Session export: one sheet per assignment ────────────────────────────

    private async Task BuildSessionSheetsAsync(
        ExcelPackage pkg, ExportJob job, IUnitOfWork uow, CancellationToken ct)
    {
        var assignments = (await uow.Assignments.FindAsync(a => a.ExamSessionId == job.ExamSessionId))
                          .OrderBy(a => a.Code).ToList();

        if (assignments.Count == 0)
        {
            pkg.Workbook.Worksheets.Add("No data");
            return;
        }

        foreach (var assignment in assignments)
            await BuildAssignmentSheetAsync(pkg, assignment.Id, job.GradingRound, uow, ct,
                sheetName: $"Mã đề {assignment.Code}");
    }

    // ── Per-assignment sheet ────────────────────────────────────────────────

    private async Task BuildAssignmentSheetAsync(
        ExcelPackage pkg, Guid assignmentId, string? gradingRound,
        IUnitOfWork uow, CancellationToken ct, string? sheetName = null)
    {
        var assignment = await uow.Assignments.GetByIdAsync(assignmentId)
                         ?? throw new InvalidOperationException($"Assignment {assignmentId} not found");

        var questions = (await uow.Questions.FindAsync(q => q.AssignmentId == assignmentId))
                        .OrderBy(q => q.CreatedAt).ToList();

        var testCaseMap = new Dictionary<Guid, List<TestCase>>();
        foreach (var q in questions)
            testCaseMap[q.Id] = (await uow.TestCases.FindAsync(tc => tc.QuestionId == q.Id))
                                 .OrderBy(tc => tc.CreatedAt).ToList();

        var submissionsQuery = await uow.Submissions.FindAsync(s => s.AssignmentId == assignmentId);
        var submissions = (gradingRound != null
            ? submissionsQuery.Where(s => s.GradingRound == gradingRound)
            : submissionsQuery).ToList();

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();

        var allGradingJobs = (await uow.GradingJobs.FindAsync(
            j => submissionIds.Contains(j.SubmissionId) && j.Status == JobStatus.Done)).ToList();

        var latestJobBySubmission = allGradingJobs
            .GroupBy(j => j.SubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(j => j.FinishedAt).First().Id);

        var latestJobIds = latestJobBySubmission.Values.ToHashSet();

        var allResults = (await uow.QuestionResults.FindAsync(r =>
            submissionIds.Contains(r.SubmissionId)
            && (r.GradingJobId == null || latestJobIds.Contains(r.GradingJobId.Value)))).ToList();

        var allNotes = (await uow.ReviewNotes.FindAsync(n =>
            submissionIds.Contains(n.SubmissionId))).ToList();

        // Build column headers
        var columns = new List<string> { "Tên", "MSSV" };
        for (int qi = 0; qi < questions.Count; qi++)
        {
            foreach (var tc in testCaseMap[questions[qi].Id])
                columns.Add($"Q{qi + 1}: {tc.Name}");
            columns.Add($"Q{qi + 1} Total");
            columns.Add($"Q{qi + 1} Adj");
        }
        columns.Add("Grand Total");
        columns.Add("Notes");

        // Build rows
        var rows = new List<List<object>>();
        foreach (var sub in submissions.OrderBy(s => StudentCode.ParseId(s.StudentCode)))
        {
            var row = new List<object>
            {
                sub.StudentCode.Length > 9 ? sub.StudentCode[..^9] : sub.StudentCode,
                StudentCode.ParseId(sub.StudentCode),
            };

            decimal grandTotal = 0; int grandMax = 0;
            latestJobBySubmission.TryGetValue(sub.Id, out var latestJobId);
            var subResults = allResults
                .Where(r => r.SubmissionId == sub.Id
                    && (latestJobId == Guid.Empty ? r.GradingJobId == null : r.GradingJobId == latestJobId))
                .ToList();

            for (int qi = 0; qi < questions.Count; qi++)
            {
                var q       = questions[qi];
                var tcs     = testCaseMap[q.Id];
                var qResult = subResults.FirstOrDefault(r => r.QuestionId == q.Id);

                if (qResult == null)
                {
                    foreach (var _ in tcs) row.Add(0);
                    row.Add($"0/{q.MaxScore}");
                    row.Add(string.Empty);
                    grandMax += q.MaxScore;
                    continue;
                }

                var details = JsonSerializer.Deserialize<List<TestCaseResult>>(qResult.Detail ?? "[]") ?? [];
                for (int ti = 0; ti < tcs.Count; ti++)
                    row.Add(details.ElementAtOrDefault(ti)?.AwardedScore ?? 0);

                row.Add($"{qResult.FinalScore}/{qResult.MaxScore}");
                row.Add(qResult.AdjustReason ?? string.Empty);
                grandTotal += qResult.FinalScore;
                grandMax   += qResult.MaxScore;
            }

            row.Add($"{grandTotal}/{grandMax}");
            row.Add(allNotes.FirstOrDefault(n => n.SubmissionId == sub.Id)?.Content ?? string.Empty);
            rows.Add(row);
        }

        var ws = pkg.Workbook.Worksheets.Add(sheetName ?? assignment.Code);

        for (int c = 0; c < columns.Count; c++)
            ws.Cells[1, c + 1].Value = columns[c];

        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Count; c++)
                ws.Cells[r + 2, c + 1].Value = rows[r][c];

        ws.Cells.AutoFitColumns();
    }
}
