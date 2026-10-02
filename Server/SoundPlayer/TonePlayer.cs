using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

public class TonePlayer : IDisposable
{
    public double MaxFrequency { get; set; } = 1000;

    public TonePlayer(
        WasapiPlayer player,
        Proto.ToneType toneType,
        double frequency,
        double gain,
        int duration,
        bool[] enabledChannels)
    {
        _signalGenerator = new SignalGenerator(48000,
            enabledChannels.Length > 0 ? 1 : 2) // mono if we are routing channels, otherwise stereo
        {
            Gain = gain,
            Frequency = frequency,
            Type = toneType switch
            {
                Proto.ToneType.Sine => SignalGeneratorType.Sin,
                Proto.ToneType.Triangle => SignalGeneratorType.Triangle,
                Proto.ToneType.Square => SignalGeneratorType.Square,
                Proto.ToneType.SawTooth => SignalGeneratorType.SawTooth,
                Proto.ToneType.Sweep => SignalGeneratorType.Sweep,
                Proto.ToneType.Pink => SignalGeneratorType.Pink,
                Proto.ToneType.White => SignalGeneratorType.White,
                _ => SignalGeneratorType.Sin
            }
        };

        if (duration > 0)
            _signalGenerator = _signalGenerator.Take(TimeSpan.FromMilliseconds(duration));

        _player = player;

        if (enabledChannels.Length == _player.OutputWaveFormat.Channels)
        {
            _channelRouter = new ChannelRouter(_signalGenerator, enabledChannels);
        }
    }

    public void Start()
    {
        if (_channelRouter != null)
        {
            _player.Init(_channelRouter);
        }
        else
        {
            _player.Init(_signalGenerator);
        }

        _player.Play();
    }

    public void Stop()
    {
        if (_player.PlaybackState == PlaybackState.Playing)
        {
            _player.Stop();
        }
    }

    public void Dispose()
    {
        _player.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Internal

    readonly WasapiPlayer _player;
    readonly ISampleProvider _signalGenerator;
    readonly ChannelRouter? _channelRouter;

    #endregion
}
