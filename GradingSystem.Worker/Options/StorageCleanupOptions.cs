namespace GradingSystem.Worker.Options;

public sealed class StorageCleanupOptions
{
    public int IntervalHours { get; set; } = 6;
    public int RetentionDays { get; set; } = 3;
}
