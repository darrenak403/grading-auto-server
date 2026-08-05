using System.Diagnostics;

namespace GradingSystem.Worker.Services;

public class QuestionApp
{
    public required Process Process { get; set; }
    public int Port { get; set; }
    // Set true when Q2 student appsettings has wrong GivenApiBaseUrl — score = 0, app not started
    public bool GivenUrlInvalid { get; set; }
    public string? GivenUrlInvalidReason { get; set; }
    // Set true when the question's folder (ArtifactFolderName) is missing from the artifact —
    // score = 0, app not started. Never falls back to a sibling question's folder, since every
    // artifact.zip written by BulkUploadService nests each question strictly under its own
    // ArtifactFolderName subfolder — there is no legitimate case of a shared/flat root.
    public bool NotSubmitted { get; set; }
    public string? NotSubmittedReason { get; set; }
}

/// <summary>
/// Giữ trạng thái các process đang chạy cho một GradingJob.
/// Mỗi Question có một app riêng (Process + Port) tương ứng với ArtifactFolderName.
/// </summary>
public class StudentContext
{
    /// <summary>questionId → (process, port) của app đó</summary>
    public Dictionary<Guid, QuestionApp> QuestionApps { get; set; } = new();

    public required string SandboxPath { get; set; }

    /// <summary>null nếu không có Q1 Api (không cần SQL Server)</summary>
    public string? DatabaseName { get; set; }

    /// <summary>Process của given API (khởi động từ given.zip), null nếu dùng GivenApiBaseUrl tĩnh</summary>
    public Process? GivenApiProcess { get; set; }

    /// <summary>Port của given API đang chạy, 0 nếu chưa khởi động</summary>
    public int GivenApiPort { get; set; }

    /// <summary>
    /// Set when an API question's student code hardcodes its own SQL connection string in
    /// OnConfiguring (ignoring the DI-configured/env-injected one). Name is the extra "shadow"
    /// database provisioned to match what the student's app actually tries to reach, so grading
    /// can proceed without modifying the student's submission. Lock is the cross-job lock
    /// guarding that database — held for the duration of this job so concurrent jobs whose apps
    /// hardcode the same server+database name don't clobber each other's data; released in
    /// CleanupAsync. Combined into one field (rather than two parallel nullables) since they are
    /// always set and read together.
    /// </summary>
    public (string Name, SemaphoreSlim Lock)? HardcodedShadowDb { get; set; }
}
