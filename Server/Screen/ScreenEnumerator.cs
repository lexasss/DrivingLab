using System.Management;
using System.Runtime.InteropServices;

namespace Server.Screen;

internal record Screen(
    int Id,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary
);

internal class ScreenEnumerator
{
    public static Screen[] EnumerateScreens()
    {
        var screens = new List<Screen>();
        var id = 0;

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (monitor, _, _, _) =>
            {
                var info = new MONITORINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>()
                };

                if (GetMonitorInfo(monitor, ref info))
                {
                    var bounds = info.rcMonitor;
                    var model = GetMonitorFromDeviceName(info.szDevice)?.Model;

                    screens.Add(new Screen(
                        Id: id++,
                        DeviceName: model != null ? $"{model} ({info.szDevice})" : info.szDevice,
                        X: bounds.Left,
                        Y: bounds.Top,
                        Width: bounds.Right - bounds.Left,
                        Height: bounds.Bottom - bounds.Top,
                        IsPrimary: (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
                }

                return true;
            },
            IntPtr.Zero);

        return screens.ToArray();
    }

    #region Internal

    record class Info(
        string DeviceID,
        string SerialNumberID,
        string Manufacturer,
        string Model
    );

    private static Info[] GetMonitors()
    {
        static string? Decode(ushort[] chars)
        {
            return chars == null ? null : new string(chars
                    .TakeWhile(c => c != 0)
                    .Select(c => (char)c)
                    .ToArray());
        }

        List<Info> monitors = [];

        var searcher = new ManagementObjectSearcher(
            @"root\wmi",
            "SELECT * FROM WmiMonitorID");
        foreach (ManagementObject obj in searcher.Get().Cast<ManagementObject>())
        {
            string model = Decode((ushort[])obj["UserFriendlyName"]) ?? "Integrated Monitor";
            string deviceId = (string)obj["InstanceName"] ?? string.Empty;
            monitors.Add(new Info(
                deviceId.Split('_')[0],  // removes weird _0 at the end
                Decode((ushort[])obj["SerialNumberID"]) ?? string.Empty,
                Decode((ushort[])obj["ManufacturerName"]) ?? "Unknown",
                model
            ));
        }

        return monitors.ToArray();
    }

    private static Info? GetMonitorFromDeviceName(string screenDeviceName)
    {
        var monitors = GetMonitors();

        int err = GetDisplayConfigBufferSizes(
            QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
            out uint pathCount,
            out uint modeCount);

        if (err != ERROR_SUCCESS)
            throw new InvalidOperationException($"GetDisplayConfigBufferSizes failed: {err}");

        var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
        var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

        err = QueryDisplayConfig(
            QUERY_DEVICE_CONFIG_FLAGS.QDC_ONLY_ACTIVE_PATHS,
            ref pathCount,
            paths,
            ref modeCount,
            modes,
            nint.Zero);

        if (err != ERROR_SUCCESS)
            throw new InvalidOperationException($"QueryDisplayConfig failed: {err}");

        foreach (var path in paths)
        {
            var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME,
                    size = Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>(),
                    adapterId = path.sourceInfo.adapterId,
                    id = path.sourceInfo.id
                }
            };

            err = DisplayConfigGetDeviceInfo(ref sourceName);

            if (err != ERROR_SUCCESS)
                continue;

            if (!string.Equals(
                    sourceName.viewGdiDeviceName,
                    screenDeviceName,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    size = Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                    adapterId = path.targetInfo.adapterId,
                    id = path.targetInfo.id
                }
            };

            err = DisplayConfigGetDeviceInfo(ref targetName);

            if (err == ERROR_SUCCESS)
            {
                string devicePath = targetName.monitorDevicePath[4..].Replace('#', '\\');  // remove \\.\ from the path start
                return monitors.FirstOrDefault(m => devicePath.StartsWith(m.DeviceID, StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    #endregion

    #region WinAPI

    const uint MONITORINFOF_PRIMARY = 0x00000001;

    private const int ERROR_SUCCESS = 0;

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(
        QUERY_DEVICE_CONFIG_FLAGS flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(
        QUERY_DEVICE_CONFIG_FLAGS flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        nint currentTopologyId);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    private enum QUERY_DEVICE_CONFIG_FLAGS : uint
    {
        QDC_ONLY_ACTIVE_PATHS = 0x00000002
    }

    private enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint
    {
        DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1,
        DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public uint scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)]
        public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] modeInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
        public int size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    private delegate bool MonitorEnumProc(
        IntPtr hMonitor,
        IntPtr hdcMonitor,
        IntPtr lprcMonitor,
        IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(
        IntPtr hdc,
        IntPtr lprcClip,
        MonitorEnumProc lpfnEnum,
        IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(
        IntPtr hMonitor,
        ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    #endregion
}
