using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PetPresence.Desktop.Activity;

public sealed class ForegroundWindowReader : IForegroundWindowReader
{
    private const int MaxTitleLength = 512;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    public ForegroundAppSnapshot? Read()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId)
        {
            return null;
        }

        string processName;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        var titleBuffer = new StringBuilder(MaxTitleLength);
        _ = GetWindowText(hwnd, titleBuffer, titleBuffer.Capacity);

        return new ForegroundAppSnapshot(
            ProcessId: (int)pid,
            ProcessName: processName,
            WindowTitle: titleBuffer.ToString(),
            CapturedAt: DateTimeOffset.UtcNow);
    }
}
