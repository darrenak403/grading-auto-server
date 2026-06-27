namespace GradingSystem.Worker.Options;

public class WorkerOptions
{
    public int PollIntervalSeconds { get; set; } = 5;

    // Leave unset (absent from config) to get a CPU-aware default instead: cores - 1,
    // clamped to [1, 8] (see ConcurrencyCalculator). This field's value (3) is only used
    // when "Worker:MaxConcurrentJobs" is explicitly present in config — Program.cs checks
    // the raw config key, not this bound value, to tell "unset" apart from "set to 3".
    public int MaxConcurrentJobs { get; set; } = 3;

    // Wall-clock cap on a single submission's full grading run (app startup through test
    // execution). 90s gives a 3-6x safety margin over the ~15-30s typical run, while bounding
    // a hung student process to a small fraction of a 300-500 submission batch's total time.
    public int SubmissionTimeoutSeconds { get; set; } = 90;
    public int ArtifactHealthCheckTimeoutSeconds { get; set; } = 15;
    public int ArtifactPortRangeStart { get; set; } = 7000;
    public int ArtifactPortRangeEnd { get; set; } = 7999;
    public string? NewmanExecutable { get; set; }
    public string BindHost { get; set; } = "127.0.0.1";

    // Lab Docker grading port ranges
    public string LabApiHost { get; set; } = "localhost";
    public int LabApiPortRangeStart { get; set; } = 15000;
    public int LabApiPortRangeEnd { get; set; } = 16000;
    public int LabDbPortRangeStart { get; set; } = 14000;
    public int LabDbPortRangeEnd { get; set; } = 14999;
    public int LabDockerHealthCheckTimeoutSeconds { get; set; } = 120;

    // Lab Docker timeouts and resource limits
    public int LabDockerBuildTimeoutSeconds { get; set; } = 300;   // 5 min for build + start
    public int LabDockerDownTimeoutSeconds { get; set; } = 30;
    public int LabDockerBuildCachePruneIntervalJobs { get; set; } = 5;
    public string LabDockerBuildCacheKeepStorage { get; set; } = "3GB";
    public int LabDockerBuildCacheFullPruneIntervalHours { get; set; } = 24;
    public string LabContainerMemoryLimit { get; set; } = "512m";
    public double LabContainerCpuLimit { get; set; } = 1.0;
}
