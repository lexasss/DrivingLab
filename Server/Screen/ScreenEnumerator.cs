using System.Runtime.InteropServices;

namespace Server.Screen;

internal record Screen(
    int Id,
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    bool IsPrimary);

internal class ScreenEnumerator
{
    public static Screen[] EnumerateScreens()
    {
        var screens = new List<Screen>();
        var id = 0;

        EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(monitor, _, _, _) =>
            {
                var info = new MONITORINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>()
                };

                if (GetMonitorInfo(monitor, ref info))
                {
                    var bounds = info.rcMonitor;

                    screens.Add(new Screen(
                        Id: id++,
                        DeviceName: info.szDevice,
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

    const uint MONITORINFOF_PRIMARY = 0x00000001;

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
