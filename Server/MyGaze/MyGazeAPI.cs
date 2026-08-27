using System.Runtime.InteropServices;
using System.Text;

namespace Server.MyGaze;

// ----------------------------------------------------------------------------
// (C) Copyright 2015, Visual Interaction GmbH 
// ----------------------------------------------------------------------------
internal static class MyGazeAPI
{

#if (x86)
        // use for 32 bit
        const string dllName = "myGazeAPI.dll";

#elif (x64)
        //use for 64 bit
        const string dllName = "myGazeAPI64.dll";
#else
    // use for 32 bit
    const string dllName = "myGazeAPI.dll";
#warning myGazeAPI library might be wrong. 32bit dll will be loaded. 
#endif

    public delegate void GetSampleCallback(SampleStruct sampleData);
    public delegate void GetEventCallback(EventStruct eventData);
    public delegate void GetTrackingMonitorCallback(ImageStruct imageData);
    public delegate void GetCalibrationPointCallback(CalibrationPointStruct calibrationPoint);

    public const int MIN_VALID_COORD = -3000;

    public const int RET_SUCCESS = 1;
    public const int RET_DATA_INVALID = 2;
    public const int RET_CALIBRATION_ABORTED = 3;
    public const int RET_SERVER_IS_RUNNING = 4;
    public const int RET_CALIBRATION_NOT_IN_PROGRESS = 5;
    public const int RET_WINDOW_IS_OPEN = 11;
    public const int RET_WINDOW_IS_CLOSED = 12;

    public const int ERR_CONNECTION_REFUSED = 100;
    public const int ERR_CONNECTION_NOT_ESTABLISHED = 101;
    public const int ERR_CALIBRATION_NOT_AVAILABLE = 102;
    public const int ERR_CALIBRATION_NOT_VALIDATED = 103;
    public const int ERR_SERVER_NOT_RUNNING = 104;
    public const int ERR_SERVER_NOT_RESPONDING = 105;
    public const int ERR_PARAMETER_INVALID = 112;
    public const int ERR_PARAMETER_CALIBRATION_INVALID = 113;
    public const int ERR_CALIBRATION_TIMEOUT = 114;
    public const int ERR_TRACKING_NOT_STABLE = 115;
    public const int ERR_SOCKET_CREATE = 121;
    public const int ERR_SOCKET_CONNECT = 122;
    public const int ERR_SOCKET_BIND = 123;
    public const int ERR_SOCKET_DELETE = 124;
    public const int ERR_SERVER_NO_RESPONSE = 131;
    public const int ERR_SERVER_VERSION_INVALID = 132;
    public const int ERR_SERVER_VERSION_UNKNOWN = 133;
    public const int ERR_FILE_ACCESS = 171;
    public const int ERR_SOCKET_ERROR = 181;
    public const int ERR_SERVER_NOT_READY = 194;
    public const int ERR_SERVER_NOT_FOUND = 201;
    public const int ERR_SERVER_PATH_NOT_FOUND = 202;
    public const int ERR_SERVER_ACCESS_DENIED = 203;
    public const int ERR_SERVER_ACCESS_INCOMPLETE = 204;
    public const int ERR_SERVER_OUT_OF_MEMORY = 205;
    public const int ERR_MULTIPLE_DEVICES = 206;
    public const int ERR_DEVICE_NOT_FOUND = 211;
    public const int ERR_DEVICE_UNKNOWN = 212;
    public const int ERR_DEVICE_CONNECTED_TO_WRONG_PORT = 213;
    public const int ERR_FEATURE_NOT_LICENSED = 250;
    public const int ERR_LICENSE_EXPIRED = 251;
    public const int ERR_DEPRECATED_FUNCTION = 300;

    public enum Ret
    {
        SUCCESS = 1,
        DATA_INVALID = 2,
        CALIBRATION_ABORTED = 3,
        SERVER_IS_RUNNING = 4,
        CALIBRATION_NOT_IN_PROGRESS = 5,
        WINDOW_IS_OPEN = 11,
        WINDOW_IS_CLOSED = 12,

        CONNECTION_REFUSED = 100,
        CONNECTION_NOT_ESTABLISHED = 101,
        CALIBRATION_NOT_AVAILABLE = 102,
        CALIBRATION_NOT_VALIDATED = 103,
        SERVER_NOT_RUNNING = 104,
        SERVER_NOT_RESPONDING = 105,
        PARAMETER_INVALID = 112,
        PARAMETER_CALIBRATION_INVALID = 113,
        CALIBRATION_TIMEOUT = 114,
        TRACKING_NOT_STABLE = 115,
        SOCKET_CREATE = 121,
        SOCKET_CONNECT = 122,
        SOCKET_BIND = 123,
        SOCKET_DELETE = 124,
        SERVER_NO_RESPONSE = 131,
        SERVER_VERSION_INVALID = 132,
        SERVER_VERSION_UNKNOWN = 133,
        FILE_ACCESS = 171,
        SOCKET_ERROR = 181,
        SERVER_NOT_READY = 194,
        SERVER_NOT_FOUND = 201,
        SERVER_PATH_NOT_FOUND = 202,
        SERVER_ACCESS_DENIED = 203,
        SERVER_ACCESS_INCOMPLETE = 204,
        SERVER_OUT_OF_MEMORY = 205,
        MULTIPLE_DEVICES = 206,
        DEVICE_NOT_FOUND = 211,
        DEVICE_UNKNOWN = 212,
        DEVICE_CONNECTED_TO_WRONG_PORT = 213,
        FEATURE_NOT_LICENSED = 250,
        LICENSE_EXPIRED = 251,
        DEPRECATED_FUNCTION = 300,
    }

    public enum CalibrationStatusEnum
    {
        unknownCalibrationStatus = 0,
        noCalibration = 1,
        validCalibration = 2,
        performingCalibration = 3
    };

    public enum ETDevice
    {
        myGaze = 2,
        myGaze_n = 8,
    };

#pragma warning disable CS0649

    public struct SystemInfoStruct
    {
        public int samplerate;
        public int iV_MajorVersion;
        public int iV_MinorVersion;
        public int iV_Buildnumber;
        public int API_MajorVersion;
        public int API_MinorVersion;
        public int API_Buildnumber;
        public ETDevice iV_ETDevice;
    };

    public struct CalibrationPointStruct
    {
        public int number;
        public int positionX;
        public int positionY;
    };

    public struct EyeDataStruct
    {
        public double gazeX;
        public double gazeY;
        public double diam;
        public double eyePositionX;
        public double eyePositionY;
        public double eyePositionZ;
    };


    public struct SampleStruct
    {
        public Int64 timestamp;
        public EyeDataStruct leftEye;
        public EyeDataStruct rightEye;
    };

    public struct EventStruct
    {
        public char eventType;
        public char eye;
        public Int64 startTime;
        public Int64 endTime;
        public Int64 duration;
        public double positionX;
        public double positionY;
    };

    public struct EyePositionStruct
    {
        public int validity;
        public double relativePositionX;
        public double relativePositionY;
        public double relativePositionZ;
        public double positionRatingX;
        public double positionRatingY;
        public double positionRatingZ;
    };

    public struct TrackingStatusStruct
    {
        public Int64 timestamp;
        public EyePositionStruct leftEye;
        public EyePositionStruct rightEye;
        public EyePositionStruct total;
    };

    public struct AccuracyStruct
    {
        public double deviationLX;
        public double deviationLY;
        public double deviationRX;
        public double deviationRY;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct CalibrationStruct
    {
        public int method;
        public int visualization;
        public int displayDevice;
        public int speed;
        public int autoAccept;
        public int foregroundColor;
        public int backgroundColor;
        public int targetShape;
        public int targetSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string targetFilename;
    };

    public struct MonitorAttachedGeometryStruct
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string setupName;
        public int stimX;
        public int stimY;
        public int redStimDistHeight;
        public int redStimDistDepth;
        public int redInclAngle;
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ImageStruct
    {
        public int imageHeight;
        public int imageWidth;
        public int imageSize;
        public IntPtr imageBuffer;
    };

    public struct DateStruct
    {
        public int day;
        public int month;
        public int year;
    };

#pragma warning restore CS0649


    [DllImport(dllName, EntryPoint = "iV_AbortCalibration")]
    public static extern int AbortCalibration();

    [DllImport(dllName, EntryPoint = "iV_AcceptCalibrationPoint")]
    public static extern int AcceptCalibrationPoint();

    [DllImport(dllName, EntryPoint = "iV_Calibrate")]
    public static extern int Calibrate();

    [DllImport(dllName, EntryPoint = "iV_ChangeCalibrationPoint")]
    public static extern int ChangeCalibrationPoint(int number, int x, int y);

    [DllImport(dllName, EntryPoint = "iV_Connect")]
    public static extern int Connect();

    [DllImport(dllName, EntryPoint = "iV_ContinueEyetracking")]
    public static extern int ContinueEyetracking();

    [DllImport(dllName, EntryPoint = "iV_DeleteMonitorAttachedGeometry")]
    public static extern int DeleteMonitorAttachedGeometry(StringBuilder name);

    [DllImport(dllName, EntryPoint = "iV_DisableProcessorHighPerformanceMode")]
    public static extern int DisableProcessorHighPerformanceMode();

    [DllImport(dllName, EntryPoint = "iV_Disconnect")]
    public static extern int Disconnect();

    [DllImport(dllName, EntryPoint = "iV_EnableProcessorHighPerformanceMode")]
    public static extern int EnableProcessorHighPerformanceMode();

    [DllImport(dllName, EntryPoint = "iV_GetAccuracy")]
    public static extern int GetAccuracy(ref AccuracyStruct accuracy);

    [DllImport(dllName, EntryPoint = "iV_GetAccuracyImage")]
    public static extern int GetAccuracyImage(ref ImageStruct image);

    [DllImport(dllName, EntryPoint = "iV_GetCalibrationParameter")]
    public static extern int GetCalibrationParameter(ref CalibrationStruct calibrationParameter);

    [DllImport(dllName, EntryPoint = "iV_GetCalibrationPoint")]
    public static extern int GetCalibrationPoint(int calibrationPointNumber, ref CalibrationPointStruct point);

    [DllImport(dllName, EntryPoint = "iV_GetCalibrationStatus")]
    public static extern int GetCalibrationStatus(ref CalibrationStatusEnum status);

    [DllImport(dllName, EntryPoint = "iV_GetCurrentCalibrationPoint")]
    public static extern int GetCurrentCalibrationPoint(ref CalibrationPointStruct point);

    [DllImport(dllName, EntryPoint = "iV_GetCurrentMonitorAttachedGeometry")]
    public static extern int GetCurrentMonitorAttachedGeometry(ref MonitorAttachedGeometryStruct geometry);

    [DllImport(dllName, EntryPoint = "iV_GetCurrentTimestamp")]
    public static extern int GetCurrentTimestamp(ref Int64 timestamp);

    [DllImport(dllName, EntryPoint = "iV_GetEvent")]
    public static extern int GetEvent(ref EventStruct eventData);

    [DllImport(dllName, EntryPoint = "iV_GetFeatureKey")]
    public static extern int GetFeatureKey(ref Int64 featureKey);

    [DllImport(dllName, EntryPoint = "iV_GetGeometryProfiles")]
    public static extern int GetGeometryProfiles(int maxSize, ref StringBuilder profiles);

    [DllImport(dllName, EntryPoint = "iV_GetLicenseDueDate")]
    public static extern int GetLicenseDueDate(ref DateStruct expiryDate);

    [DllImport(dllName, EntryPoint = "iV_GetMonitorAttachedGeometry")]
    public static extern int GetMonitorAttachedGeometry(StringBuilder profile, ref MonitorAttachedGeometryStruct geometry);

    [DllImport(dllName, EntryPoint = "iV_GetSample")]
    public static extern int GetSample(ref SampleStruct sample);

    [DllImport(dllName, EntryPoint = "iV_GetSerialNumber")]
    public static extern int GetSerialNumber(ref StringBuilder serialNumber);

    [DllImport(dllName, EntryPoint = "iV_GetSystemInfo")]
    public static extern int GetSystemInfo(ref SystemInfoStruct systemInfo);

    [DllImport(dllName, EntryPoint = "iV_GetTrackingMonitor")]
    public static extern int GetTrackingMonitor(ref ImageStruct image);

    [DllImport(dllName, EntryPoint = "iV_GetTrackingStatus")]
    public static extern int GetTrackingStatus(ref TrackingStatusStruct trackingStatus);

    [DllImport(dllName, EntryPoint = "iV_HideAccuracyMonitor")]
    public static extern int HideAccuracyMonitor();

    [DllImport(dllName, EntryPoint = "iV_HideTrackingMonitor")]
    public static extern int HideTrackingMonitor();

    [DllImport(dllName, EntryPoint = "iV_IsConnected")]
    public static extern int IsConnected();

    [DllImport(dllName, EntryPoint = "iV_IsTrackingStable")]
    public static extern int IsTrackingStable();

    [DllImport(dllName, EntryPoint = "iV_LoadCalibration")]
    public static extern int LoadCalibration(StringBuilder name);

    [DllImport(dllName, EntryPoint = "iV_PauseEyetracking")]
    public static extern int PauseEyetracking();

    [DllImport(dllName, EntryPoint = "iV_Quit")]
    public static extern int Quit();

    [DllImport(dllName, EntryPoint = "iV_ResetCalibrationPoints")]
    public static extern int ResetCalibrationPoints();

    [DllImport(dllName, EntryPoint = "iV_SaveCalibration")]
    public static extern int SaveCalibration(StringBuilder name);

    [DllImport(dllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "iV_SetCalibrationCallback")]
    public static extern int SetCalibrationCallback(MulticastDelegate calibrationPointCallbackFunction);

    [DllImport(dllName, EntryPoint = "iV_SetConnectionTimeout")]
    public static extern int SetConnectionTimeout(int time);

    [DllImport(dllName, EntryPoint = "iV_SetGeometryProfile")]
    public static extern int SetGeometryProfile(StringBuilder profile);

    [DllImport(dllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "iV_SetEventCallback")]
    public static extern void SetEventCallback(MulticastDelegate eventCallbackFunction);

    [DllImport(dllName, EntryPoint = "iV_SetEventDetectionParameter")]
    public static extern int SetEventDetectionParameter(int minDuration, int maxDispersion);

    [DllImport(dllName, EntryPoint = "iV_SetLicense")]
    public static extern int SetLicense(StringBuilder licenseKey);

    [DllImport(dllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "iV_SetSampleCallback")]
    public static extern void SetSampleCallback(MulticastDelegate? sampleCallbackFunction);

    [DllImport(dllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "iV_SetTrackingMonitorCallback")]
    public static extern void SetTrackingMonitorCallback(MulticastDelegate trackingMonitorCallbackFunction);

    [DllImport(dllName, EntryPoint = "iV_SetTrackingParameter")]
    public static extern int SetTrackingParameter(int eye, int parameter, int reserved);

    [DllImport(dllName, EntryPoint = "iV_SetupCalibration")]
    public static extern int SetupCalibration(ref CalibrationStruct calibrationParameter);

    [DllImport(dllName, EntryPoint = "iV_SetupMonitorAttachedGeometry")]
    public static extern int SetupMonitorAttachedGeometry(ref MonitorAttachedGeometryStruct geometry);

    [DllImport(dllName, EntryPoint = "iV_ShowAccuracyMonitor")]
    public static extern int ShowAccuracyMonitor();

    [DllImport(dllName, EntryPoint = "iV_ShowTrackingMonitor")]
    public static extern int ShowTrackingMonitor();

    [DllImport(dllName, EntryPoint = "iV_Start")]
    public static extern int Start();

    [DllImport(dllName, EntryPoint = "iV_Validate")]
    public static extern int Validate();
}
