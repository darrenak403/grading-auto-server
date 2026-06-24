using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using GradingSystem.Application.Common;
using GradingSystem.Application.Interfaces;
using GradingSystem.Domain.Entities;
using OfficeOpenXml;

namespace GradingSystem.Worker.Services;

public partial class ExportRunner(
    IConfiguration config,
    ILogger<ExportRunner> logger)
{
    public async Task<string> GenerateAsync(ExportJob job, IUnitOfWork uow, CancellationToken ct)
    {
        using var pkg = new ExcelPackage();

        if (job.ExamSessionId.HasValue)
            await BuildSessionSheetsAsync(pkg, job, uow, ct);
        else if (job.AssignmentId.HasValue)
            await BuildAssignmentSheetAsync(pkg, job.AssignmentId.Value, job.GradingRound, uow, ct);
        else if (job.LabAssignmentId.HasValue)
            await BuildLabAssignmentSheetAsync(pkg, job.LabAssignmentId.Value, uow, ct);
        else
            throw new InvalidOperationException(
                "ExportJob has no AssignmentId, ExamSessionId, or LabAssignmentId.");

        var dir = Path.GetFullPath(Path.Combine(config["Storage:BasePath"]!, "exports"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{job.Id}.xlsx");
        await pkg.SaveAsAsync(new FileInfo(path), ct);

        logger.LogInformation("Export {JobId} saved to {Path}", job.Id, path);
        return path;
    }

    private async Task BuildSessionSheetsAsync(
        ExcelPackage pkg, ExportJob job, IUnitOfWork uow, CancellationToken ct)
    {
        var assignments = (await uow.Assignments.FindAsync(a => a.ExamSessionId == job.ExamSessionId))
            .OrderBy(a => a.Code).ToList();

        if (assignments.Count == 0)
        {
            pkg.Workbook.Worksheets.Add(UniqueSheetName(pkg, "No data"));
            return;
        }

        foreach (var assignment in assignments)
            await BuildAssignmentSheetAsync(pkg, assignment.Id, job.GradingRound, uow, ct,
                sheetName: $"Mã đề {assignment.Code}");
    }

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

        var rows = new List<List<object>>();
        foreach (var sub in submissions.OrderBy(s => StudentCode.ParseId(s.StudentCode)))
        {
            var row = new List<object>
            {
                sub.StudentCode.Length > 9 ? sub.StudentCode[..^9] : sub.StudentCode,
                StudentCode.ParseId(sub.StudentCode),
            };

            decimal grandTotal = 0;
            int grandMax = 0;
            latestJobBySubmission.TryGetValue(sub.Id, out var latestJobId);
            var subResults = allResults
                .Where(r => r.SubmissionId == sub.Id
                    && (latestJobId == Guid.Empty ? r.GradingJobId == null : r.GradingJobId == latestJobId))
                .ToList();

            for (int qi = 0; qi < questions.Count; qi++)
            {
                var q = questions[qi];
                var tcs = testCaseMap[q.Id];
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
                grandMax += qResult.MaxScore;
            }

            row.Add($"{grandTotal}/{grandMax}");
            row.Add(allNotes.FirstOrDefault(n => n.SubmissionId == sub.Id)?.Content ?? string.Empty);
            rows.Add(row);
        }

        WriteSheet(pkg, UniqueSheetName(pkg, sheetName ?? assignment.Code), columns, rows);
    }

    private async Task BuildLabAssignmentSheetAsync(
        ExcelPackage pkg, Guid labAssignmentId, IUnitOfWork uow, CancellationToken ct)
    {
        var assignment = await uow.LabAssignments.GetByIdAsync(labAssignmentId)
            ?? throw new InvalidOperationException($"LabAssignment {labAssignmentId} not found");

        var testCases = (await uow.LabTestCases.FindAsync(t =>
                t.LabAssignmentId == labAssignmentId && t.Status == LabTestCaseStatus.Approved))
            .OrderBy(t => t.Order).ThenBy(t => t.CreatedAt).ToList();

        var submissions = (await uow.LabSubmissions.FindAsync(s => s.LabAssignmentId == labAssignmentId))
            .OrderBy(s => StudentCode.ParseId(s.StudentCode)).ToList();

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();
        var jobs = await uow.LabGradingJobs.FindAsync(j => submissionIds.Contains(j.LabSubmissionId));

        var latestJobBySubmission = jobs
            .GroupBy(j => j.LabSubmissionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(j => j.CreatedAt).ThenByDescending(j => j.Id).First());

        var latestJobIds = latestJobBySubmission.Values.Select(j => j.Id).ToHashSet();
        var allResults = latestJobIds.Count == 0
            ? []
            : await uow.LabTestCaseResults.FindAsync(r => latestJobIds.Contains(r.LabGradingJobId));

        var resultsByJobAndTestCase = allResults.ToDictionary(r => (r.LabGradingJobId, r.LabTestCaseId));
        var maxScore = testCases.Sum(t => t.Score);

        var columns = new List<string> { "Tên", "MSSV" };
        foreach (var tc in testCases)
            columns.Add(LabTestCaseLabel(tc));
        columns.Add("Grand Total");
        columns.Add("Status");

        var rows = new List<List<object>>();
        foreach (var sub in submissions)
        {
            var row = new List<object>
            {
                sub.StudentCode.Length > 9 ? sub.StudentCode[..^9] : sub.StudentCode,
                StudentCode.ParseId(sub.StudentCode),
            };

            decimal grandTotal = 0;
            latestJobBySubmission.TryGetValue(sub.Id, out var latestJob);

            foreach (var tc in testCases)
            {
                if (latestJob is null ||
                    !resultsByJobAndTestCase.TryGetValue((latestJob.Id, tc.Id), out var result))
                {
                    row.Add(0);
                    continue;
                }

                var score = result.ManualOverrideScore ?? result.AwardedScore;
                row.Add(FormatLabTestCaseCell(score, tc, result));
                grandTotal += score;
            }

            row.Add($"{grandTotal}/{maxScore}");
            row.Add(sub.Status.ToString());
            rows.Add(row);
        }

        var sheetName = UniqueSheetName(pkg, assignment.Title);
        var ws = WriteSheet(pkg, sheetName, columns, rows);

        for (var c = 3; c < 3 + testCases.Count; c++)
        {
            ws.Column(c).Width = 35;
            ws.Column(c).Style.WrapText = true;
        }

        if (rows.Count > 0 && testCases.Count > 0)
        {
            ws.Cells[2, 3, rows.Count + 1, 2 + testCases.Count].Style.VerticalAlignment =
                OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
        }

        ApplyLabTestCaseScoreStyles(ws, submissions, testCases, latestJobBySubmission, resultsByJobAndTestCase);
    }

    private static ExcelWorksheet WriteSheet(
        ExcelPackage pkg, string sheetName, IReadOnlyList<string> columns, IReadOnlyList<List<object>> rows)
    {
        var ws = pkg.Workbook.Worksheets.Add(sheetName);

        for (int c = 0; c < columns.Count; c++)
            ws.Cells[1, c + 1].Value = columns[c];

        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Count; c++)
                ws.Cells[r + 2, c + 1].Value = rows[r][c];

        ws.Cells.AutoFitColumns();
        return ws;
    }

    private static string LabTestCaseLabel(LabTestCase tc)
    {
        if (!string.IsNullOrWhiteSpace(tc.Description))
            return tc.Description;
        if (tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase))
            return "SOURCE";
        return $"{tc.HttpMethod} {tc.UrlTemplate}";
    }

    private static string FormatLabTestCaseCell(decimal score, LabTestCase tc, LabTestCaseResult result)
    {
        var lines = new List<string> { $"Điểm: {score}" };

        lines.Add(tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase)
            ? $"Rule: {tc.UrlTemplate}"
            : $"Endpoint: {tc.HttpMethod} {tc.UrlTemplate}");

        if (result.ActualStatusCode.HasValue)
            lines.Add($"Status: {result.ActualStatusCode}");

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            lines.Add($"Lỗi: {NormalizeExcelText(result.ErrorMessage)}");

        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeExcelText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
        return TruncateExcelText(normalized);
    }

    private static string TruncateExcelText(string value)
    {
        const int maxLength = 2000;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }

    private static void ApplyLabTestCaseScoreStyles(
        ExcelWorksheet ws,
        IReadOnlyList<LabSubmission> submissions,
        IReadOnlyList<LabTestCase> testCases,
        IReadOnlyDictionary<Guid, LabGradingJob> latestJobBySubmission,
        IReadOnlyDictionary<(Guid LabGradingJobId, Guid LabTestCaseId), LabTestCaseResult> resultsByJobAndTestCase)
    {
        for (var rowIndex = 0; rowIndex < submissions.Count; rowIndex++)
        {
            var submission = submissions[rowIndex];
            if (!latestJobBySubmission.TryGetValue(submission.Id, out var latestJob))
                continue;

            for (var testCaseIndex = 0; testCaseIndex < testCases.Count; testCaseIndex++)
            {
                var testCase = testCases[testCaseIndex];
                if (!resultsByJobAndTestCase.TryGetValue((latestJob.Id, testCase.Id), out var result))
                    continue;

                var score = result.ManualOverrideScore ?? result.AwardedScore;
                var cell = ws.Cells[rowIndex + 2, testCaseIndex + 3];
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;

                if (score >= testCase.Score)
                {
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 239, 218));
                    cell.Style.Font.Color.SetColor(Color.FromArgb(0, 97, 0));
                }
                else
                {
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 199, 206));
                    cell.Style.Font.Color.SetColor(Color.FromArgb(156, 0, 6));
                }
            }
        }
    }

    [GeneratedRegex(@"[:\\/?*\[\]]")]
    private static partial Regex InvalidSheetNameCharsRegex();

    private static string SanitizeSheetName(string name)
    {
        var sanitized = InvalidSheetNameCharsRegex().Replace(name.Trim(), "_");
        if (sanitized.Length > 31)
            sanitized = sanitized[..31];
        return string.IsNullOrWhiteSpace(sanitized) ? "Sheet1" : sanitized;
    }

    private static string UniqueSheetName(ExcelPackage pkg, string baseName)
    {
        var name = SanitizeSheetName(baseName);
        if (pkg.Workbook.Worksheets.All(w => w.Name != name))
            return name;

        for (var i = 2; i < 100; i++)
        {
            var suffix = $"_{i}";
            var candidate = SanitizeSheetName(
                baseName.Length + suffix.Length > 31
                    ? baseName[..Math.Max(1, 31 - suffix.Length)] + suffix
                    : baseName + suffix);
            if (pkg.Workbook.Worksheets.All(w => w.Name != candidate))
                return candidate;
        }

        return SanitizeSheetName(Guid.NewGuid().ToString("N")[..8]);
    }
}
