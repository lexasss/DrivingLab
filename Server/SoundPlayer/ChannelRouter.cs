using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Server.SoundPlayer;

public sealed class ChannelRouter : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    public ChannelRouter(
        ISampleProvider source,
        bool[] enabledChannels)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        if (enabledChannels == null)
            throw new ArgumentNullException(nameof(enabledChannels));

        if (source.WaveFormat.Channels != 1)
            throw new ArgumentException(
                "The source must be mono.",
                nameof(source));

        _source = source;
        _enabledChannels = (bool[])enabledChannels.Clone();

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            source.WaveFormat.SampleRate,
            _enabledChannels.Length);
    }

    public int Read(Span<float> buffer)
    {
        int framesRequested = buffer.Length / 8;

        if (framesRequested == 0)
            return 0;

        float[] monoBuffer = new float[framesRequested];

        int samplesRead = _source.Read(monoBuffer);

        int outputIndex = 0;

        for (int frame = 0; frame < samplesRead; frame++)
        {
            float sample = monoBuffer[frame];

            for (int channel = 0; channel < _enabledChannels.Length; channel++)
            {
                buffer[outputIndex++] =
                    _enabledChannels[channel]
                        ? sample
                        : 0.0f;
            }
        }

        return samplesRead * _enabledChannels.Length;
    }

    public static void Test()
    {
        var signal = new SignalGenerator(48000, 1)
        {
            Frequency = 1000,
            Gain = 0.2,
            Type = SignalGeneratorType.Sin
        };

        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        using var device = devices.FirstOrDefault(d => d.FriendlyName.Contains("Logitech"));
        using WasapiPlayer? soundPlayer = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithEventSync()
            .WithLatency(50)
            .WithCategory(AudioStreamCategory.Media)
            .WithRawMode()
            .Build();

        var channelCount = soundPlayer.OutputWaveFormat.Channels;

        bool[] selectedChannels = new bool[channelCount];
        selectedChannels[1] = true;

        var router = new ChannelRouter(
            signal,
            selectedChannels);

        soundPlayer.Init(router);
        soundPlayer.Play();

        Console.WriteLine("Playing...");
        Console.ReadLine();
    }

    #region Internal

    readonly ISampleProvider _source;
    readonly bool[] _enabledChannels;

    #endregion
}