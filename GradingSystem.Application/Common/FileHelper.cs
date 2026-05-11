namespace GradingSystem.Application.Common;

public static class FileHelper
{
    public static void SafeDelete(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            File.Delete(path);
            var dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                Directory.Delete(dir);
        }
        catch { /* ignore IO errors */ }
    }
}
