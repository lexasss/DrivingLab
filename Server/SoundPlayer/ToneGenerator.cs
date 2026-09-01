using NAudio.Wave;
using Proto = global::SoundPlayer;

namespace Server.SoundPlayer;

internal record class Harmonic(int Index, double Gain, double Phase);

internal class ToneGenerator : ISampleProvider
{
    public WaveFormat WaveFormat { get; }

    /// <summary>
    /// (-20000.0 - 20000.0 Hz)
    /// Negative values affect left channel, positive values affect right channel
    /// </summary>
    public double Frequency { get; set; } = 0.0;

    /// <summary>
    /// 0..1
    /// </summary>
    public double Gain { get; set; } = 1;

    public Proto.ToneType ToneType { get; set; } = Proto.ToneType.Sine;

    /// <summary>
    /// In miliseconds. Zero means continuos tone. Avoid long pulse durations (>200ms)
    /// </summary>
    public int PulseDuration { get; set; } = 0;

    /// <summary>
    /// Used to generate the tone of <see cref="ToneType.Harmonics"/> type
    /// </summary>
    public Harmonic[] Harmonics { get; set; } = [
        new Harmonic(1, 1, 0),
        new Harmonic(2, 0.61, Math.PI / 3),
        new Harmonic(3, 0.39, Math.PI / 4),
        new Harmonic(4, 0.21, Math.PI / 7),
        new Harmonic(5, 0.11, Math.PI / 2),
    ];

    /// <summary>
    /// Initializes a new instance for the Generator
    /// </summary>
    /// <param name="sampleRate">Desired sample rate</param>
    public ToneGenerator(int sampleRate = 44100)
    {
        _stepDuration = 1000d / sampleRate;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
        _totalGain = Harmonics.Sum(h => h.Gain);
    }

    public void Reset()
    {
        _accDuration = 0;
        _mode = PulseDuration == 0 ? Mode.Continuos : Mode.Off;
    }

    /// <summary>
    /// Reads from this provider.
    /// </summary>
    public int Read(float[] buffer, int offset, int count)
    {
        int outIndex = offset;
        int sampleCount = count / WaveFormat.Channels;

        var freq = _mode == Mode.Continuos ? Math.Abs(Frequency) : PULSE_FREQUENCY;

        //_tonePulseInterval = Math.Min(MAX_PULSE_INTERVAL, 50 * MAX_PULSE_INTERVAL / Math.Abs(Frequency));
        _tonePulseInterval = Math.Min(MAX_PULSE_INTERVAL, MAX_PULSE_INTERVAL / Math.Exp(0.005 * Math.Abs(Frequency)));

        var freqLeft = Frequency < 0 ? freq : 0;
        double stepLeft = TWO_PI * freqLeft / WaveFormat.SampleRate;

        var freqRight = Frequency > 0 ? freq : 0;
        double stepRight = TWO_PI * freqRight / WaveFormat.SampleRate;

        for (int si = 0; si < sampleCount; si++)
        {
            _accDuration += _stepDuration;
            if (_mode == Mode.Off && _accDuration > _tonePulseInterval)
            {
                _mode = Mode.On;
                _accDuration = 0;
            }
            else if (_mode == Mode.On && _accDuration > PulseDuration)
            {
                _mode = Mode.Off;
                _accDuration = 0;
            }

            for (int ci = 0; ci < WaveFormat.Channels; ci++)
            {
                var phase = (ci == 0 ? _phaseLeft : _phaseRight) % TWO_PI;
                double sampleValue = (ToneType switch
                {
                    Proto.ToneType.Sine => Math.Sin(phase),
                    Proto.ToneType.Triangle => (phase > Math.PI ? TWO_PI - phase : phase) / PI_2 - 1,
                    Proto.ToneType.Harmonics => Harmonics.Sum(h => h.Gain * Math.Sin(h.Phase + h.Index * phase)) / _totalGain,
                    _ => throw new NotImplementedException("This tone type is not supported")
                }) * Gain;

                buffer[outIndex++] = (float)sampleValue;
            }

            if (_mode != Mode.Off)
            {
                _phaseLeft += stepLeft;
                _phaseRight += stepRight;
            }
        }

        return count;
    }

    public int Read(Span<float> buffer)
    {
        return Read(buffer.ToArray(), 0, buffer.Length);
    }

    // Internal

    enum Mode { Continuos, Off, On }

    const double TWO_PI = 2 * Math.PI;
    const double PI_2 = Math.PI / 2;

    const double PULSE_FREQUENCY = 200;
    const double MAX_PULSE_INTERVAL = 3000;

    readonly double _stepDuration;  // ms
    readonly double _totalGain;

    static double _phaseLeft = 0;
    static double _phaseRight = 0;

    static double _accDuration = 0;

    double _tonePulseInterval;
    Mode _mode = Mode.Continuos;
}