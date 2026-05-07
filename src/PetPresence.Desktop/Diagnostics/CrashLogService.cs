using System.Text.RegularExpressions;

namespace PetPresence.Desktop.Diagnostics;

public sealed class CrashLogService
{
    private readonly string _logDirectory;

    public CrashLogService(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PetPresence",
            "CrashLogs");
    }

    public void RegisterGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                Write(exception, "UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Write(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };
    }

    public string Write(Exception exception, string source)
    {
        Directory.CreateDirectory(_logDirectory);
        var filePath = Path.Combine(_logDirectory, $"crash-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.log");
        var sanitized = Sanitize($"Source: {source}\n{exception}");
        File.WriteAllText(filePath, sanitized);
        return filePath;
    }

    // Sanitizes foreground metadata labels such as process name and window title before writing local crash logs.
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sanitized = Regex.Replace(
            text,
            "(?im)^(.*?(process name|processname|window title|windowtitle|raw title|address|query)\\s*[:=]).*$",
            "$1 [redacted]");

        sanitized = Regex.Replace(sanitized, "(?i)https?://\\S+", "[redacted-link]");
        return sanitized;
    }
}
