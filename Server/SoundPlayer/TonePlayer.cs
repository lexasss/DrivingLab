using NAudio.Wave;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

public class SoundDevice(string id, string name)
{
    public string Id => id;
    public string Name => name;
    public override string ToString() => name;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class TonePlayer : IDisposable
{
    public double MaxFrequency { get; set; } = 1000;

    public TonePlayer(
        WasapiPlayer player,
        Proto.ToneType toneType,
        double frequency,
        double gain,
        int pulseDuration)
    {
        _toneGenerator = new ToneGenerator() {
            Frequency = frequency,
            Gain = gain,
            ToneType = toneType,
            PulseDuration = pulseDuration
        };

        _player = player;
    }

    public void Start()
    {
        _toneGenerator.Frequency = 0;
        _toneGenerator.Reset();

        _player.Init(_toneGenerator);
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

    /// <summary>
    /// Sets the sine frequence from 0 Hz to <see cref="MaxFrequency"/> Hz,
    /// or affects the pulse interval if <see cref="TonePulseDuration"/> is >0.
    /// </summary>
    /// <param name="factor">-1..1: negative parameter values affect the left channel,
    /// and positive values affect the right channel</param>
    public void SetPitchFactor(double factor)
    {
        _toneGenerator.Frequency = factor * MaxFrequency;
        //_toneGenerator.Frequency = Math.Sign(factor) * Math.Exp(Math.Abs(factor) * 3.5 - 2.5) * MaxFrequency / Math.E;
    }

    #region Internal

    readonly WasapiPlayer _player;
    readonly ToneGenerator _toneGenerator;

    #endregion
}
