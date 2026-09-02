using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class TonePlayer : IDisposable
{
    public double MaxFrequency { get; set; } = 1000;

    public TonePlayer(
        WasapiPlayer player,
        Proto.ToneType toneType,
        double frequency,
        double gain,
        int duration)
    {
        _signalGenerator = new SignalGenerator()
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
    }

    public void Start()
    {
        _player.Init(_signalGenerator);
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

    #endregion
}
