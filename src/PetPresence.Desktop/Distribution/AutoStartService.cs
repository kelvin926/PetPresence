using Microsoft.Win32;

namespace PetPresence.Desktop.Distribution;

public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PetPresence";

    public bool IsEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            key.SetValue(AppName, Quote(executablePath));
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    private static string Quote(string executablePath) => $"\"{executablePath}\" --background";
}
