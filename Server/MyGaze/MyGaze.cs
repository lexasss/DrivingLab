using Microsoft.Extensions.Logging;
using System.Text;

namespace Server.MyGaze;

internal class MyGaze : IDisposable
{
	public event EventHandler<MyGazeAPI.SampleStruct>? Sample;

	public bool IsConnected { get; private set; }
	public bool IsTracking { get; private set; }

	public MyGaze(ILogger<MyGazeService> logger)
	{
		_logger = logger;

        Log(MyGazeAPI.SetLicense(LICENSE), nameof(MyGazeAPI.SetLicense));
        MyGazeAPI.Ret result = Log(MyGazeAPI.Connect(), nameof(MyGazeAPI.Connect));

        if (result == MyGazeAPI.Ret.SUCCESS)
        {
			var info = new MyGazeAPI.SystemInfoStruct();
            _ = MyGazeAPI.GetSystemInfo(ref info);

            _logger.LogInformation($"[VIMG] {info.iV_ETDevice} v{info.iV_MajorVersion}.{info.iV_MinorVersion}.{info.iV_Buildnumber} @ {info.samplerate} Hz [API v{info.API_MajorVersion}.{info.API_MinorVersion}.{info.API_Buildnumber}]");
        }

		IsConnected = MyGazeAPI.IsConnected() == MyGazeAPI.RET_SUCCESS;

		if (IsConnected)
		{
            Log(MyGazeAPI.Start(), nameof(MyGazeAPI.Start));
		}
	}

	public void Start()
    {
		if (IsConnected && !IsTracking)
		{
			MyGazeAPI.SetSampleCallback(new MyGazeAPI.GetSampleCallback(GetSampleCallbackFunction));
			IsTracking = true;
		}
	}

	public void Stop()
	{
		if (IsTracking)
		{
			MyGazeAPI.SetSampleCallback(null);
			IsTracking = false;
        }
    }

	public void Dispose()
	{
        Log(MyGazeAPI.Disconnect(), nameof(MyGazeAPI.Disconnect));
        Log(MyGazeAPI.Quit(), nameof(MyGazeAPI.Quit));
	}

	#region Internal

	readonly StringBuilder LICENSE = new("NBBwa2iQ1Iu3eLwt");

	readonly ILogger<MyGazeService> _logger;

    private void GetSampleCallbackFunction(MyGazeAPI.SampleStruct sample)
	{
		Sample?.Invoke(this, sample);
	}

	private MyGazeAPI.Ret Log(int result, string fnc)
    {
		MyGazeAPI.Ret code = (MyGazeAPI.Ret)result;
        _logger.LogInformation("[VIMG] {fnc} => {code}", fnc, code);
		return code;
	}

    #endregion
}
