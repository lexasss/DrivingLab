using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Runtime.InteropServices;

namespace Server.SoundPlayer;

public sealed class ChannelRouter : IWaveProvider
{
    public WaveFormat WaveFormat { get; }

    public ChannelRouter(
        ISampleProvider source,
        bool[] enabledChannels,
        WaveFormat? format = null)
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

        WaveFormat = format ?? WaveFormat.CreateIeeeFloatWaveFormat(
            _source.WaveFormat.SampleRate,
            _enabledChannels.Length);
    }

    public int Read(Span<byte> buffer)
    {
        int samplesRequested = buffer.Length / WaveFormat.Channels / sizeof(float);

        if (samplesRequested == 0)
            return 0;

        float[] monoBuffer = new float[samplesRequested];

        int samplesRead = _source.Read(monoBuffer);

        int outputIndex = 0;
        var zero = new FloatBytes() { Float = 0f };

        for (int frame = 0; frame < samplesRead; frame++)
        {
            float sample = monoBuffer[frame];

            for (int channel = 0; channel < _enabledChannels.Length; channel++)
            {
                if (!_enabledChannels[channel])
                {
                    buffer[outputIndex++] = zero.B1;
                    buffer[outputIndex++] = zero.B2;
                    buffer[outputIndex++] = zero.B3;
                    buffer[outputIndex++] = zero.B4;
                }
                else
                {
                    var fb = new FloatBytes() { Float = sample };
                    buffer[outputIndex++] = fb.B1;
                    buffer[outputIndex++] = fb.B2;
                    buffer[outputIndex++] = fb.B3;
                    buffer[outputIndex++] = fb.B4;
                }
            }
        }

        return samplesRead * _enabledChannels.Length * sizeof(float);
    }

    public static async Task Test()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var devices = enumerator.EnumerateAudioEndPoints(
            DataFlow.Render,
            DeviceState.Active);
        using var device = devices.FirstOrDefault(d => d.FriendlyName.Contains("MOTU pro audio", StringComparison.OrdinalIgnoreCase));

        WasapiPlayer soundPlayer = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithEventSync()
            .WithLatency(50)
            .WithCategory(AudioStreamCategory.Media)
            .Build();
        var sf = soundPlayer.DeviceMixFormat;
        Console.WriteLine($"Supported format : channels = {sf.Channels}, encoding = {sf.Encoding}, sample rate = {sf.SampleRate}, block align = {sf.BlockAlign}, bps = {sf.BitsPerSample}");

        var channelCount = soundPlayer.DeviceMixFormat.Channels;
        int sampleRate = 48000;

        for (int i = 0; i < channelCount; i++)
        {
            bool[] selectedChannels = new bool[channelCount];
            selectedChannels[i] = true;

            var signal = new SignalGenerator(sampleRate, 1)
            {
                Frequency = 1000,
                Gain = 0.2,
                Type = SignalGeneratorType.Sin
            }.Take(TimeSpan.FromSeconds(1));

            var router = new ChannelRouter(
                signal,
                selectedChannels,
                soundPlayer.DeviceMixFormat);

            if (!soundPlayer!.IsFormatSupported(router.WaveFormat, out WaveFormatExtensible bmf))
            {
                var uf = router.WaveFormat;
                Console.WriteLine($"Unsupported format : channels = {uf.Channels}, encoding = {uf.Encoding}, sample rate = {uf.SampleRate}, block align = {uf.BlockAlign}, bps = {uf.BitsPerSample}");
                if (bmf != null)
                    Console.WriteLine($"Try this format : channels = {bmf.Channels}, encoding = {bmf.Encoding}, sample rate = {bmf.SampleRate}, block align = {bmf.BlockAlign}, bps = {bmf.BitsPerSample}");
                return;
            }

            Console.WriteLine($"Playing on channel {i+1}");

            try
            {
                soundPlayer.Init(router);
                soundPlayer.Play();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  failed: {ex.Message}");
                var uf = router.WaveFormat;
                Console.WriteLine($"Unsupported format : channels = {uf.Channels}, encoding = {uf.Encoding}, sample rate = {uf.SampleRate}, block align = {uf.BlockAlign}, bps = {uf.BitsPerSample}");
                return;
            }

            await Task.Delay(1000);
            soundPlayer?.Dispose();

            if (i < channelCount - 1)
            {
                await Task.Delay(1000);
                soundPlayer = new WasapiPlayerBuilder()
                            .WithDevice(device)
                            .WithEventSync()
                            .WithLatency(50)
                            .WithCategory(AudioStreamCategory.Media)
                            .Build();
            }
        }
    }

    #region Internal

    [StructLayout(LayoutKind.Explicit)]
    struct FloatBytes
    {
        [FieldOffset(0)]
        public float Float;

        [FieldOffset(0)]
        public byte B1;

        [FieldOffset(1)]
        public byte B2;
        [FieldOffset(2)]
        public byte B3;
        [FieldOffset(3)]
        public byte B4;
    }

    readonly ISampleProvider _source;
    readonly bool[] _enabledChannels;

    #endregion
}
