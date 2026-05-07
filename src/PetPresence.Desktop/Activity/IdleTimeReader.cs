using System.Runtime.InteropServices;

namespace PetPresence.Desktop.Activity;

public sealed class IdleTimeReader : IIdleTimeReader
{
    public TimeSpan GetIdleTime()
    {
        if (!OperatingSystem.IsWindows())
        {
            return TimeSpan.Zero;
        }

        var info = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var elapsedMilliseconds = GetTickCount64() - info.dwTime;
        return TimeSpan.FromMilliseconds(Math.Max(0, elapsedMilliseconds));
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
}
