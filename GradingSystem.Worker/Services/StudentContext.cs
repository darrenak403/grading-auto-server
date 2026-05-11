using System.Diagnostics;

namespace GradingSystem.Worker.Services;

public class QuestionApp
{
    public required Process Process { get; set; }
    public int Port { get; set; }
    // Set true when Q2 student appsettings has wrong GivenApiBaseUrl — score = 0, app not started
    public bool GivenUrlInvalid { get; set; }
    public string? GivenUrlInvalidReason { get; set; }
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
}
