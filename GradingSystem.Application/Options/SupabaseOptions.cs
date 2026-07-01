namespace GradingSystem.Application.Options;

public class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    public bool AutoSync { get; set; } = false;
}
