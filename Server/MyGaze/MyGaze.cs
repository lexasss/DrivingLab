using Microsoft.Extensions.Logging;
using System.Text;

namespace Server.MyGaze;

internal class MyGaze : IDisposable
{
	public event EventHandler<MyGazeAPI.SampleStruct>? Sample;
    public event EventHandler<MyGazeAPI.EventStruct>? Event;

    public bool IsConnected { get; private set; }
	public bool IsTracking { get; private set; }

	public MyGaze(ILogger logger)
	{
		_logger = logger;

        Log(MyGazeAPI.SetLicense(LICENSE), nameof(MyGazeAPI.SetLicense));
        MyGazeAPI.Result result = Log(MyGazeAPI.Connect(), nameof(MyGazeAPI.Connect));

        if (result == MyGazeAPI.Result.Success)
        {
			var info = new MyGazeAPI.SystemInfoStruct();
            _ = MyGazeAPI.GetSystemInfo(ref info);

			MyGazeAPI.SetEventCallback(GetEventCallbackFunction);

            _logger.LogInformation($"{info.iV_ETDevice} v{info.iV_MajorVersion}.{info.iV_MinorVersion}.{info.iV_Buildnumber} @ {info.samplerate} Hz [API v{info.API_MajorVersion}.{info.API_MinorVersion}.{info.API_Buildnumber}]");
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
        MyGazeAPI.Disconnect();
        MyGazeAPI.Quit();
	}

	#region Internal

	readonly StringBuilder LICENSE = new("NBBwa2iQ1Iu3eLwt");

	readonly ILogger _logger;

    private void GetSampleCallbackFunction(MyGazeAPI.SampleStruct sample)
	{
		Sample?.Invoke(this, sample);
	}

	private void GetEventCallbackFunction(MyGazeAPI.EventStruct evt)
	{
		Event?.Invoke(this, evt);
	}

    private MyGazeAPI.Result Log(int result, string fnc)
    {
		MyGazeAPI.Result code = (MyGazeAPI.Result)result;
		if (MyGazeAPI.IsError(code))
            _logger.LogError("{fnc} => {code}", fnc, code);
		else
            _logger.LogInformation("{fnc} => {code}", fnc, code);
        return code;
	}

    #endregion
}
