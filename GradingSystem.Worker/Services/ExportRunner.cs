using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using GradingSystem.Application.Common;
using GradingSystem.Application.Exceptions;
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
        List<Assignment> markInputAssignments = [];

        if (job.ExamSessionId.HasValue)
            markInputAssignments = await BuildSessionSheetsAsync(pkg, job, uow, ct);
        else if (job.AssignmentId.HasValue)
            markInputAssignments = [await BuildAssignmentSheetAsync(pkg, job.AssignmentId.Value, job.GradingRound, uow, ct)];
        else if (job.LabAssignmentId.HasValue)
            await BuildLabAssignmentSheetAsync(pkg, job.LabAssignmentId.Value, uow, ct);
        else
            throw new InvalidOperationException(
                "ExportJob has no AssignmentId, ExamSessionId, or LabAssignmentId.");

        if (markInputAssignments.Count > 0)
            await BuildMarkInputSheetAsync(pkg, markInputAssignments, job.GradingRound, uow, ct);

        var dir = Path.GetFullPath(Path.Combine(config["Storage:BasePath"]!, "exports"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{job.Id}.xlsx");
        await pkg.SaveAsAsync(new FileInfo(path), ct);

        logger.LogInformation("Export {JobId} saved to {Path}", job.Id, path);
        return path;
    }

    private async Task<List<Assignment>> BuildSessionSheetsAsync(
        ExcelPackage pkg, ExportJob job, IUnitOfWork uow, CancellationToken ct)
    {
        var allAssignments = (await uow.Assignments.FindAsync(a => a.ExamSessionId == job.ExamSessionId))
            .OrderBy(a => a.Code).ToList();
        var assignments = job.AssignmentId.HasValue
            ? allAssignments.Where(a => a.Id == job.AssignmentId.Value).ToList()
            : allAssignments;

        if (assignments.Count == 0)
        {
            if (job.AssignmentId.HasValue)
                logger.LogWarning(
                    "Export job {JobId} filtered to Assignment {AssignmentId} but it no longer belongs to ExamSession {ExamSessionId}",
                    job.Id, job.AssignmentId, job.ExamSessionId);
            pkg.Workbook.Worksheets.Add(UniqueSheetName(pkg, "No data"));
            return [];
        }

        foreach (var assignment in assignments)
            await BuildAssignmentSheetAsync(pkg, assignment.Id, job.GradingRound, uow, ct,
                sheetName: $"Mã đề {assignment.Code}");

        return assignments;
    }

    private async Task<Assignment> BuildAssignmentSheetAsync(
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
        if (gradingRound is null && submissionsQuery.Select(s => s.GradingRound).Distinct().Count() > 1)
            throw new BadRequestException(
                $"Assignment {assignmentId} has multiple grading rounds; specify gradingRound to export.");

        var submissions = (gradingRound != null
            ? submissionsQuery.Where(s => s.GradingRound == gradingRound)
            : submissionsQuery).ToList();

        var submissionIds = submissions.Select(s => s.Id).ToHashSet();

        var participantIds = submissions.Where(s => s.ParticipantId.HasValue)
            .Select(s => s.ParticipantId!.Value).ToHashSet();
        var participantsById = participantIds.Count == 0
            ? new Dictionary<Guid, Participant>()
            : (await uow.Participants.FindAsync(p => participantIds.Contains(p.Id)))
                .ToDictionary(p => p.Id);

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
            columns.Add($"Q{qi + 1} Note");
        }
        columns.Add("Grand Total");
        columns.Add("Notes");

        var rows = new List<List<object>>();
        foreach (var sub in submissions.OrderBy(s => s.StudentCode))
        {
            participantsById.TryGetValue(sub.ParticipantId ?? Guid.Empty, out var participant);
            var row = new List<object>
            {
                participant?.Username ?? sub.StudentCode,
                participant?.StudentCode ?? sub.StudentCode,
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
                    row.Add(string.Empty);
                    grandMax += q.MaxScore;
                    continue;
                }

                var details = JsonSerializer.Deserialize<List<TestCaseResult>>(qResult.Detail ?? "[]") ?? [];
                for (int ti = 0; ti < tcs.Count; ti++)
                    row.Add(details.ElementAtOrDefault(ti)?.AwardedScore ?? 0);

                row.Add($"{qResult.FinalScore}/{qResult.MaxScore}");
                row.Add(qResult.AdjustReason ?? string.Empty);

                // Basic Vietnamese per-question note: name which test cases inside this question
                // came up short, so the grader can see why without opening the JSON detail.
                var failedTestCaseNames = tcs
                    .Where((tc, ti) => (details.ElementAtOrDefault(ti)?.AwardedScore ?? 0) < tc.Score)
                    .Select(tc => tc.Name)
                    .ToList();
                row.Add(failedTestCaseNames.Count > 0 ? $"Sai: {string.Join(", ", failedTestCaseNames)}" : string.Empty);

                grandTotal += qResult.FinalScore;
                grandMax += qResult.MaxScore;
            }

            row.Add($"{grandTotal}/{grandMax}");
            row.Add(allNotes.FirstOrDefault(n => n.SubmissionId == sub.Id)?.Content ?? string.Empty);
            rows.Add(row);
        }

        WriteSheet(pkg, UniqueSheetName(pkg, sheetName ?? assignment.Code), columns, rows);
        return assignment;
    }

    /// Builds the "Mark_Input" sheet in the exact layout the department's official mark-input
    /// template uses (PRN323_SU26_FE_PE_Mark_Input.xlsx): No./Solution/Paper/Marker, one column
    /// per question holding its final score, a Total formula column, and a Comment column — all
    /// mã đề combined into one continuously-numbered sheet, matching the template row-for-row.
    private async Task BuildMarkInputSheetAsync(
        ExcelPackage pkg, IReadOnlyList<Assignment> assignments, string? gradingRound,
        IUnitOfWork uow, CancellationToken ct)
    {
        var rows = new List<MarkInputRow>();
        var questionMaxScores = new List<int>();

        foreach (var assignment in assignments.OrderBy(a => a.Code))
        {
            var questions = (await uow.Questions.FindAsync(q => q.AssignmentId == assignment.Id))
                .OrderBy(q => q.CreatedAt).ToList();
            if (questions.Count > questionMaxScores.Count)
            {
                questionMaxScores.Clear();
                questionMaxScores.AddRange(questions.Select(q => q.MaxScore));
            }

            var submissionsQuery = await uow.Submissions.FindAsync(s => s.AssignmentId == assignment.Id);
            var submissions = (gradingRound != null
                ? submissionsQuery.Where(s => s.GradingRound == gradingRound)
                : submissionsQuery).OrderBy(s => s.StudentCode).ToList();
            var submissionIds = submissions.Select(s => s.Id).ToHashSet();

            var participantIds = submissions.Where(s => s.ParticipantId.HasValue)
                .Select(s => s.ParticipantId!.Value).ToHashSet();
            var participantsById = participantIds.Count == 0
                ? new Dictionary<Guid, Participant>()
                : (await uow.Participants.FindAsync(p => participantIds.Contains(p.Id))).ToDictionary(p => p.Id);

            var allGradingJobs = (await uow.GradingJobs.FindAsync(
                j => submissionIds.Contains(j.SubmissionId) && j.Status == JobStatus.Done)).ToList();
            var latestJobBySubmission = allGradingJobs
                .GroupBy(j => j.SubmissionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(j => j.FinishedAt).First().Id);
            var latestJobIds = latestJobBySubmission.Values.ToHashSet();

            var allResults = (await uow.QuestionResults.FindAsync(r =>
                submissionIds.Contains(r.SubmissionId)
                && (r.GradingJobId == null || latestJobIds.Contains(r.GradingJobId.Value)))).ToList();

            // Latest grading attempt regardless of outcome, used only to detect whether the most
            // recent run for a submission failed outright (so the Comment can explain why instead
            // of just listing wrong questions — its ErrorMessage is already a Vietnamese explanation,
            // see GradingPipeline.TranslateJobError).
            var latestAttemptBySubmission = (await uow.GradingJobs.FindAsync(
                    j => submissionIds.Contains(j.SubmissionId)
                        && (j.Status == JobStatus.Done || j.Status == JobStatus.Failed)))
                .GroupBy(j => j.SubmissionId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(j => j.FinishedAt).First());

            var allNotes = (await uow.ReviewNotes.FindAsync(n => submissionIds.Contains(n.SubmissionId))).ToList();

            foreach (var sub in submissions)
            {
                participantsById.TryGetValue(sub.ParticipantId ?? Guid.Empty, out var participant);
                latestJobBySubmission.TryGetValue(sub.Id, out var latestJobId);

                var subResults = allResults
                    .Where(r => r.SubmissionId == sub.Id
                        && (latestJobId == Guid.Empty ? r.GradingJobId == null : r.GradingJobId == latestJobId))
                    .ToList();

                var scores = questions
                    .Select(q => subResults.FirstOrDefault(r => r.QuestionId == q.Id)?.FinalScore ?? 0m)
                    .ToList();

                var manualNote = allNotes.FirstOrDefault(n => n.SubmissionId == sub.Id)?.Content;
                var autoComment = BuildMarkInputComment(questions, subResults, latestAttemptBySubmission, sub.Id);
                var comment = string.Join(" | ", new[] { autoComment, manualNote }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));

                rows.Add(new MarkInputRow(
                    participant?.Username ?? sub.StudentCode,
                    assignment.Code,
                    scores,
                    comment));
            }
        }

        WriteMarkInputSheet(pkg, rows, questionMaxScores);
    }

    private sealed record MarkInputRow(
        string Solution, string Paper, IReadOnlyList<decimal> QuestionScores, string Comment);

    // Basic Vietnamese auto-comment: if the latest grading attempt failed outright, surface why
    // (job.ErrorMessage is already a Vietnamese explanation — see GradingPipeline.TranslateJobError);
    // otherwise just name which question numbers came up short, per grader request — no detail needed.
    private static string BuildMarkInputComment(
        IReadOnlyList<Question> questions, IReadOnlyList<QuestionResult> subResults,
        IReadOnlyDictionary<Guid, GradingJob> latestAttemptBySubmission, Guid submissionId)
    {
        if (latestAttemptBySubmission.TryGetValue(submissionId, out var latestAttempt)
            && latestAttempt.Status == JobStatus.Failed)
            return latestAttempt.ErrorMessage ?? "Lỗi hệ thống khi chấm bài, cần chấm lại.";

        var wrongQuestionNumbers = questions
            .Select((q, qi) => (q, qi))
            .Where(t => (subResults.FirstOrDefault(r => r.QuestionId == t.q.Id)?.FinalScore ?? 0m) < t.q.MaxScore)
            .Select(t => (t.qi + 1).ToString())
            .ToList();

        return wrongQuestionNumbers.Count > 0
            ? $"Sai câu: {string.Join(", ", wrongQuestionNumbers)}"
            : string.Empty;
    }

    private static void WriteMarkInputSheet(
        ExcelPackage pkg, IReadOnlyList<MarkInputRow> rows, IReadOnlyList<int> questionMaxScores)
    {
        var ws = pkg.Workbook.Worksheets.Add(UniqueSheetName(pkg, "Mark_Input"));

        const int noCol = 1, solutionCol = 2, paperCol = 3, markerCol = 4, firstQuestionCol = 5;
        var questionCount = Math.Max(questionMaxScores.Count, 1);
        var totalCol = firstQuestionCol + questionCount;
        var commentCol = totalCol + 1;

        var headerFill = Color.FromArgb(0xED, 0x7D, 0x31);
        var commentHeaderFill = Color.FromArgb(0xFF, 0xFF, 0x00);

        OfficeOpenXml.Style.ExcelHorizontalAlignment? ColumnAlign(int col) => col switch
        {
            noCol or paperCol or markerCol => OfficeOpenXml.Style.ExcelHorizontalAlignment.Center,
            solutionCol => OfficeOpenXml.Style.ExcelHorizontalAlignment.Left,
            _ when col >= firstQuestionCol && col <= totalCol => OfficeOpenXml.Style.ExcelHorizontalAlignment.Center,
            _ => null,
        };

        void StyleCell(int row, int col, bool bold, Color? fill)
        {
            var cell = ws.Cells[row, col];
            cell.Style.Font.Name = "Aptos";
            cell.Style.Font.Size = 11;
            cell.Style.Font.Bold = bold;
            if (fill.HasValue)
            {
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(fill.Value);
            }
            cell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            var align = ColumnAlign(col);
            if (align.HasValue)
                cell.Style.HorizontalAlignment = align.Value;
        }

        // Row 1: "Question N" labels over each question column, "Total" over the total column.
        for (var col = 1; col <= commentCol; col++)
        {
            StyleCell(1, col, bold: true, headerFill);
            if (col >= firstQuestionCol && col < totalCol)
                ws.Cells[1, col].Value = $"Question {col - firstQuestionCol + 1}";
            else if (col == totalCol)
                ws.Cells[1, col].Value = "Total";
        }

        // Row 2: column labels, per-question max score, and the max-score Total formula.
        ws.Cells[2, noCol].Value = "No.";
        ws.Cells[2, solutionCol].Value = "Solution";
        ws.Cells[2, paperCol].Value = "Paper";
        ws.Cells[2, markerCol].Value = "Marker";
        for (var qi = 0; qi < questionCount; qi++)
            ws.Cells[2, firstQuestionCol + qi].Value = qi < questionMaxScores.Count ? questionMaxScores[qi] : 0;
        ws.Cells[2, totalCol].Formula = $"SUM({ws.Cells[2, firstQuestionCol, 2, totalCol - 1].Address.Replace("$", "")})";
        ws.Cells[2, commentCol].Value = "Comment";
        for (var col = 1; col <= commentCol; col++)
            StyleCell(2, col, bold: true, col == commentCol ? commentHeaderFill : headerFill);

        // Data rows.
        for (var i = 0; i < rows.Count; i++)
        {
            var row = i + 3;
            var r = rows[i];

            ws.Cells[row, noCol].Value = i + 1;
            ws.Cells[row, solutionCol].Value = r.Solution;
            ws.Cells[row, paperCol].Value = int.TryParse(r.Paper, out var paperNum) ? paperNum : r.Paper;
            for (var qi = 0; qi < questionCount; qi++)
                ws.Cells[row, firstQuestionCol + qi].Value = qi < r.QuestionScores.Count ? r.QuestionScores[qi] : 0m;
            ws.Cells[row, totalCol].Formula =
                $"SUM({ws.Cells[row, firstQuestionCol, row, totalCol - 1].Address.Replace("$", "")})";
            ws.Cells[row, commentCol].Value = r.Comment;

            for (var col = 1; col <= commentCol; col++)
                StyleCell(row, col, bold: col == totalCol, fill: null);
        }

        ws.Column(noCol).Width = 8.63;
        ws.Column(solutionCol).Width = 20.36;
        ws.Column(paperCol).Width = 11;
        ws.Column(markerCol).Width = 13;
        ws.Column(firstQuestionCol).Width = 17.18;
        ws.Column(totalCol).Width = 17.63;
        ws.Column(commentCol).Width = 58.54;

        ws.View.FreezePanes(3, firstQuestionCol);
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
        var loginCredentials = ExtractLabLoginCredentials(testCases);

        var columns = new List<string> { "Tên", "MSSV", "Login Username", "Login Password" };
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
                loginCredentials.Username,
                loginCredentials.Password,
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

        for (var c = 5; c < 5 + testCases.Count; c++)
        {
            ws.Column(c).Width = 35;
            ws.Column(c).Style.WrapText = true;
        }

        if (rows.Count > 0 && testCases.Count > 0)
        {
            ws.Cells[2, 5, rows.Count + 1, 4 + testCases.Count].Style.VerticalAlignment =
                OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
        }

        ApplyLabTestCaseScoreStyles(ws, submissions, testCases, latestJobBySubmission, resultsByJobAndTestCase, firstTestCaseColumn: 5);
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

    private static LabLoginCredentials ExtractLabLoginCredentials(IReadOnlyList<LabTestCase> testCases)
    {
        var loginCase = testCases.FirstOrDefault(IsLoginTestCase);

        if (loginCase?.InputJson is null)
            return new LabLoginCredentials(string.Empty, string.Empty);

        return ExtractLoginCredentials(loginCase.InputJson);
    }

    private static bool IsLoginTestCase(LabTestCase tc) =>
        tc.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase)
        && tc.UrlTemplate.Contains("/auth/login", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(tc.InputJson);

    private static LabLoginCredentials ExtractLoginCredentials(string? inputJson)
    {
        if (string.IsNullOrWhiteSpace(inputJson))
            return new LabLoginCredentials(string.Empty, string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(inputJson);
            var root = doc.RootElement;
            var username = TryGetStringProperty(root, "username") ?? TryGetStringProperty(root, "userName") ?? string.Empty;
            var password = TryGetStringProperty(root, "password") ?? string.Empty;
            return new LabLoginCredentials(username, password);
        }
        catch
        {
            return new LabLoginCredentials(string.Empty, string.Empty);
        }
    }

    private static string? TryGetStringProperty(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var value))
            return null;

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static string FormatLabTestCaseCell(decimal score, LabTestCase tc, LabTestCaseResult result)
    {
        var lines = new List<string> { $"Điểm: {score}" };

        lines.Add(tc.HttpMethod.Equals("SOURCE", StringComparison.OrdinalIgnoreCase)
            ? $"Rule: {tc.UrlTemplate}"
            : $"Endpoint: {tc.HttpMethod} {tc.UrlTemplate}");

        if (IsLoginTestCase(tc))
        {
            var credentials = ExtractLoginCredentials(tc.InputJson);
            if (!string.IsNullOrWhiteSpace(credentials.Username))
                lines.Add($"Username: {credentials.Username}");
            if (!string.IsNullOrWhiteSpace(credentials.Password))
                lines.Add($"Password: {credentials.Password}");
        }

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
        IReadOnlyDictionary<(Guid LabGradingJobId, Guid LabTestCaseId), LabTestCaseResult> resultsByJobAndTestCase,
        int firstTestCaseColumn = 3)
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
                var cell = ws.Cells[rowIndex + 2, testCaseIndex + firstTestCaseColumn];
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

    private sealed record LabLoginCredentials(string Username, string Password);

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
