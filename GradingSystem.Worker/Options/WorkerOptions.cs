namespace GradingSystem.Worker.Options;

public class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;
    public int MaxConcurrentJobs { get; set; } = 3;
    public int ArtifactHealthCheckTimeoutSeconds { get; set; } = 15;
    public int ArtifactPortRangeStart { get; set; } = 7000;
    public int ArtifactPortRangeEnd { get; set; } = 7999;
    public string? NewmanExecutable { get; set; }
    public string BindHost { get; set; } = "127.0.0.1";

    // Lab Docker grading port ranges
    public int LabApiPortRangeStart { get; set; } = 15000;
    public int LabApiPortRangeEnd { get; set; } = 16000;
    public int LabDbPortRangeStart { get; set; } = 14000;
    public int LabDbPortRangeEnd { get; set; } = 14999;
    public int LabDockerHealthCheckTimeoutSeconds { get; set; } = 60;

    // Lab Docker timeouts and resource limits
    public int LabDockerBuildTimeoutSeconds { get; set; } = 300;   // 5 min for build + start
    public int LabDockerDownTimeoutSeconds { get; set; } = 30;
    public string LabContainerMemoryLimit { get; set; } = "512m";
    public double LabContainerCpuLimit { get; set; } = 1.0;
}
