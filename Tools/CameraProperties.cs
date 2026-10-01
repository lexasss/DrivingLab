using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Tools;

public class CameraProperties : IDisposable
{
    public enum CameraControlProperty
    {
        Pan = 0,
        Tilt,
        Roll,
        Zoom,
        Exposure,
        Iris,
        Focus
    }

    public record class ControlRange(long Min, long Max, long Step, long DefaultValue, bool CanBeAuto, bool CanSetManually);

    public CameraProperties(string symbolicLink)
    {
        var type = Type.GetTypeFromCLSID(CLSID_SystemDeviceEnum);
        if (type == null)
            return;

        var devEnum = (DirectN.ICreateDevEnum)Activator.CreateInstance(type)!;
        if (devEnum == null)
            return;

        var hr = devEnum.CreateClassEnumerator(
            Guid.Parse("{860BB310-5D01-11D0-BD3B-00A0C911CE86}"),
            out IEnumMoniker enumMoniker,
            0);

        if (hr != DirectN.Constants.STATUS_SUCCESS || enumMoniker == null)
            return;

        IMoniker[] monikers = new IMoniker[1];
        nint count = 0;

        while (enumMoniker.Next(1, monikers, count) == DirectN.Constants.STATUS_SUCCESS)
        {
            var moniker = monikers[0];
            try
            {
                var guid = typeof(IPropertyBag).GUID;
                moniker.BindToStorage(null!, null, ref guid, out object? obj);
                var bag = (IPropertyBag)obj;
                var value = bag.Read("DevicePath", out object? pvar, null!);

                if (pvar is string devicePath &&
                    devicePath.StartsWith(
                        symbolicLink.Substring(0, 50),
                        StringComparison.OrdinalIgnoreCase)) // check only device ID
                {
                    _moniker = moniker;
                    break;
                }
            }
            finally
            {
                if (moniker != null)
                    Marshal.ReleaseComObject(moniker);
            }
        }

        Marshal.ReleaseComObject(enumMoniker);
        Marshal.ReleaseComObject(devEnum);
    }

    public bool GetControlRangeExposure(CameraControlProperty controlProp, out ControlRange? range)
    {
        range = null;

        if (_moniker == null)
            return false;

        try
        {
            var guid = typeof(DirectN.IBaseFilter).GUID;
            _moniker.BindToObject(null!, null, ref guid, out object? obj);
            if (obj is DirectN.IBaseFilter filter)
            {
                var cameraControl = (DirectN.IAMCameraControl)filter;

                var hr = cameraControl.GetRange(
                    (int)controlProp,
                    out long min,
                    out long max,
                    out long step,
                    out long def,
                    out long caps);

                Marshal.ThrowExceptionForHR((int)hr);

                int minimum = (int)min;
                int maximum = (int)max;
                int defaultValue = (int)def;
                var capabilities = (CameraControlFlags)caps;
                range = new ControlRange(minimum, maximum, step, defaultValue,
                    capabilities.HasFlag(CameraControlFlags.Auto),
                    capabilities.HasFlag(CameraControlFlags.Manual));
            }

            if (obj != null)
                Marshal.ReleaseComObject(obj);
        }
        catch
        {
            return false;
        }

        return true;
    }

    public void SetControlValue(CameraControlProperty controlProp, int value)
    {
        if (_moniker == null)
            return;

        var guid = typeof(DirectN.IBaseFilter).GUID;
        _moniker.BindToObject(null!, null, ref guid, out object? obj);
        if (obj is DirectN.IBaseFilter filter)
        {
            var cameraControl = (DirectN.IAMCameraControl)filter;
            cameraControl.Set(
                (int)controlProp,
                value,
                (int)CameraControlFlags.Manual);
        }

        if (obj != null)
            Marshal.ReleaseComObject(obj);
    }

    public void Dispose()
    {
        if (_moniker != null)
            Marshal.ReleaseComObject(_moniker);
    }

    IMoniker? _moniker = null;

    static readonly Guid CLSID_SystemDeviceEnum = Guid.Parse("{62BE5D10-60EB-11D0-BD3B-00A0C911CE86}");

    [Flags]
    enum CameraControlFlags
    {
        None = 0,
        Auto = 0x0001,
        Manual = 0x0002
    }

    [ComImport]
    [Guid("55272A00-42CB-11CE-8135-00AA004BB851")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyBag
    {
        [PreserveSig]
        int Read(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
          [Out, MarshalAs(UnmanagedType.Struct)] out object pVar,
          [In] DirectN.IErrorLog pErrorLog
        );

        [PreserveSig]
        int Write(
          [In, MarshalAs(UnmanagedType.LPWStr)] string pszPropName,
          [In, MarshalAs(UnmanagedType.Struct)] ref object pVar
        );
    }
}
