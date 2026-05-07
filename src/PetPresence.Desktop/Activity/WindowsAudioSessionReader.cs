using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PetPresence.Desktop.Activity;

public sealed class WindowsAudioSessionReader : IAudioSessionReader
{
    private const float ActivePeakThreshold = 0.005f;

    public IReadOnlyList<AudioActivitySnapshot> ReadActiveSessions()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var snapshots = new List<AudioActivitySnapshot>();
        try
        {
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumerator();
            enumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            var iid = typeof(IAudioSessionManager2).GUID;
            device.Activate(ref iid, CLSCTX.CLSCTX_ALL, IntPtr.Zero, out var managerObject);
            var manager = (IAudioSessionManager2)managerObject;
            manager.GetSessionEnumerator(out var sessionEnumerator);
            sessionEnumerator.GetCount(out var count);

            for (var index = 0; index < count; index++)
            {
                sessionEnumerator.GetSession(index, out var control);
                if (control is not IAudioSessionControl2 control2)
                {
                    continue;
                }

                control2.GetProcessId(out var processId);
                if (processId == 0)
                {
                    continue;
                }

                var peakValue = 0f;
                if (control is IAudioMeterInformation meter)
                {
                    meter.GetPeakValue(out peakValue);
                }

                if (peakValue < ActivePeakThreshold)
                {
                    continue;
                }

                string processName;
                try
                {
                    processName = Process.GetProcessById((int)processId).ProcessName;
                }
                catch (ArgumentException)
                {
                    continue;
                }

                snapshots.Add(new AudioActivitySnapshot((int)processId, processName, peakValue, DateTimeOffset.UtcNow));
            }
        }
        catch (COMException)
        {
            return snapshots;
        }
        catch (InvalidCastException)
        {
            return snapshots;
        }

        return snapshots;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator
    {
    }

    private enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    private enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [Flags]
    private enum CLSCTX
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
        CLSCTX_REMOTE_SERVER = 0x10,
        CLSCTX_ALL = CLSCTX_INPROC_SERVER | CLSCTX_INPROC_HANDLER | CLSCTX_LOCAL_SERVER | CLSCTX_REMOTE_SERVER
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints();
        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, CLSCTX dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        void GetAudioSessionControl();
        void GetSimpleAudioVolume();
        void GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator);
        void RegisterSessionNotification();
        void UnregisterSessionNotification();
        void RegisterDuckNotification();
        void UnregisterDuckNotification();
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        void GetCount(out int sessionCount);
        void GetSession(int sessionIndex, out IAudioSessionControl sessionControl);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        void GetState(out int state);
        void GetDisplayName(out IntPtr displayName);
        void SetDisplayName();
        void GetIconPath(out IntPtr iconPath);
        void SetIconPath();
        void GetGroupingParam(out Guid groupingParam);
        void SetGroupingParam();
        void RegisterAudioSessionNotification();
        void UnregisterAudioSessionNotification();
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        void GetState(out int state);
        void GetDisplayName(out IntPtr displayName);
        void SetDisplayName();
        void GetIconPath(out IntPtr iconPath);
        void SetIconPath();
        void GetGroupingParam(out Guid groupingParam);
        void SetGroupingParam();
        void RegisterAudioSessionNotification();
        void UnregisterAudioSessionNotification();
        void GetSessionIdentifier(out IntPtr retVal);
        void GetSessionInstanceIdentifier(out IntPtr retVal);
        void GetProcessId(out uint processId);
        void IsSystemSoundsSession();
        void SetDuckingPreference(bool optOut);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        void GetPeakValue(out float peak);
        void GetMeteringChannelCount(out int channelCount);
        void GetChannelsPeakValues(int channelCount, [Out] float[] peaks);
        void QueryHardwareSupport(out int hardwareSupportMask);
    }
}
