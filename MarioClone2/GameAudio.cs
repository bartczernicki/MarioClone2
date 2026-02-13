using System.Text;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MarioClone2;

// Lightweight procedural audio system built on a single software mixer.
// Music is looped continuously while SFX are mixed in as one-shot layers.
internal sealed class GameAudio : IDisposable
{
    // MixingSampleProvider is not thread-safe for concurrent AddMixerInput calls.
    private readonly object _mixerLock = new();
    private WaveOutEvent? _outputDevice;
    private MixingSampleProvider? _mixer;
    private CachedSound? _jumpSound;
    private CachedSound? _squishSound;
    private CachedSound? _brickBreakSound;
    private CachedSound? _powerupSound;
    private CachedSound? _shrinkSound;
    private bool _enabled;

    public GameAudio()
    {
        try
        {
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(22050, 1))
            {
                ReadFully = true
            };

            var music = new CachedSound(WaveFactory.CreateBackgroundMusicWave(), 0.42f);
            _jumpSound = new CachedSound(WaveFactory.CreateJumpWave(), 0.88f);
            _squishSound = new CachedSound(WaveFactory.CreateSquishWave(), 0.86f);
            _brickBreakSound = new CachedSound(WaveFactory.CreateBrickBreakWave(), 0.8f);
            _powerupSound = new CachedSound(WaveFactory.CreatePowerupWave(), 0.88f);
            _shrinkSound = new CachedSound(WaveFactory.CreateShrinkWave(), 0.9f);

            _mixer.AddMixerInput(new LoopingCachedSoundSampleProvider(music));

            _outputDevice = new WaveOutEvent();
            _outputDevice.Init(_mixer);
            _enabled = true;
        }
        catch
        {
            _enabled = false;
            _outputDevice?.Dispose();
            _outputDevice = null;
            _mixer = null;
        }
    }

    public void PlayMusic()
    {
        if (!_enabled || _outputDevice is null)
        {
            return;
        }

        if (_outputDevice.PlaybackState != PlaybackState.Playing)
        {
            _outputDevice.Play();
        }
    }

    public void PlayJump()
    {
        PlayOneShot(_jumpSound);
    }

    public void PlaySquish()
    {
        PlayOneShot(_squishSound);
    }

    public void PlayBrickBreak()
    {
        PlayOneShot(_brickBreakSound);
    }

    public void PlayPowerup()
    {
        PlayOneShot(_powerupSound);
    }

    public void PlayShrink()
    {
        PlayOneShot(_shrinkSound);
    }

    public void Dispose()
    {
        try
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
        }
        catch
        {
            // Audio failures are non-fatal.
        }
    }

    private void PlayOneShot(CachedSound? sound)
    {
        if (!_enabled || _mixer is null || sound is null)
        {
            return;
        }

        lock (_mixerLock)
        {
            _mixer.AddMixerInput(new CachedSoundSampleProvider(sound));
        }
    }
}

internal static class WaveFactory
{
    private const int SampleRate = 22050;

    public static byte[] CreateBackgroundMusicWave()
    {
        var b = new WaveBuilder(SampleRate);
        const float beat = 0.2f;
        var notes = new (float freq, float beats)[]
        {
            (392f, 2f), (523f, 1f), (659f, 1f), (784f, 2f), (659f, 2f),
            (523f, 1f), (587f, 1f), (659f, 2f), (523f, 2f), (392f, 2f),
            (440f, 2f), (587f, 1f), (698f, 1f), (784f, 2f), (698f, 2f),
            (587f, 1f), (659f, 1f), (698f, 2f), (587f, 2f), (392f, 2f)
        };

        foreach (var (freq, beats) in notes)
        {
            b.AddSine(freq, beats * beat, 0.09f);
            b.AddSine(freq * 0.5f, beats * beat, 0.028f);
            b.AddSilence(0.008f);
        }

        return b.ToWaveBytes();
    }

    public static byte[] CreateJumpWave()
    {
        var b = new WaveBuilder(SampleRate);
        b.AddSine(620f, 0.05f, 0.24f);
        b.AddSine(880f, 0.09f, 0.2f);
        return b.ToWaveBytes();
    }

    public static byte[] CreateSquishWave()
    {
        var b = new WaveBuilder(SampleRate);
        b.AddSquare(260f, 0.04f, 0.2f);
        b.AddSquare(170f, 0.08f, 0.18f);
        return b.ToWaveBytes();
    }

    public static byte[] CreateBrickBreakWave()
    {
        var b = new WaveBuilder(SampleRate);
        b.AddNoise(0.1f, 0.24f);
        b.AddSquare(120f, 0.05f, 0.08f);
        return b.ToWaveBytes();
    }

    public static byte[] CreatePowerupWave()
    {
        var b = new WaveBuilder(SampleRate);
        b.AddSine(523f, 0.05f, 0.2f);
        b.AddSine(659f, 0.05f, 0.2f);
        b.AddSine(784f, 0.07f, 0.2f);
        b.AddSine(988f, 0.09f, 0.18f);
        return b.ToWaveBytes();
    }

    public static byte[] CreateShrinkWave()
    {
        var b = new WaveBuilder(SampleRate);
        // Descending chirp implies the player shrinking.
        b.AddSquare(780f, 0.03f, 0.18f);
        b.AddSquare(520f, 0.035f, 0.17f);
        b.AddSquare(340f, 0.05f, 0.16f);
        return b.ToWaveBytes();
    }

    private sealed class WaveBuilder
    {
        private readonly int _sampleRate;
        private readonly List<short> _samples = [];
        private readonly Random _rng = new(19);

        public WaveBuilder(int sampleRate)
        {
            _sampleRate = sampleRate;
        }

        public void AddSine(float frequency, float durationSeconds, float volume)
        {
            AddTone(frequency, durationSeconds, volume, square: false);
        }

        public void AddSquare(float frequency, float durationSeconds, float volume)
        {
            AddTone(frequency, durationSeconds, volume, square: true);
        }

        public void AddNoise(float durationSeconds, float volume)
        {
            var sampleCount = Math.Max(1, (int)(_sampleRate * durationSeconds));
            var releaseSamples = Math.Max(1, (int)(_sampleRate * 0.02f));

            for (var i = 0; i < sampleCount; i++)
            {
                var env = 1f;
                if (i >= sampleCount - releaseSamples)
                {
                    env = (sampleCount - i) / (float)releaseSamples;
                }

                var value = ((float)(_rng.NextDouble() * 2.0 - 1.0)) * volume * env;
                _samples.Add(ToPcm16(value));
            }
        }

        public void AddSilence(float durationSeconds)
        {
            var sampleCount = Math.Max(1, (int)(_sampleRate * durationSeconds));
            for (var i = 0; i < sampleCount; i++)
            {
                _samples.Add(0);
            }
        }

        public byte[] ToWaveBytes()
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

            var dataSize = _samples.Count * sizeof(short);
            var byteRate = _sampleRate * sizeof(short);
            const short channels = 1;
            const short bitsPerSample = 16;
            const short blockAlign = sizeof(short);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(_sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            foreach (var sample in _samples)
            {
                writer.Write(sample);
            }

            writer.Flush();
            return ms.ToArray();
        }

        private void AddTone(float frequency, float durationSeconds, float volume, bool square)
        {
            var sampleCount = Math.Max(1, (int)(_sampleRate * durationSeconds));
            var attackSamples = Math.Max(1, (int)(_sampleRate * 0.004f));
            var releaseSamples = Math.Max(1, (int)(_sampleRate * 0.02f));

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)_sampleRate;
                var sin = MathF.Sin(2f * MathF.PI * frequency * t);
                var wave = square ? MathF.Sign(sin) : sin;

                var env = 1f;
                if (i < attackSamples)
                {
                    env = i / (float)attackSamples;
                }
                else if (i >= sampleCount - releaseSamples)
                {
                    env = (sampleCount - i) / (float)releaseSamples;
                }

                var value = wave * volume * env;
                _samples.Add(ToPcm16(value));
            }
        }

        private static short ToPcm16(float sample)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            return (short)(clamped * short.MaxValue);
        }
    }
}

internal sealed class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    public CachedSound(byte[] waveData, float gain = 1f)
    {
        using var ms = new MemoryStream(waveData, writable: false);
        using var reader = new WaveFileReader(ms);
        var sampleProvider = reader.ToSampleProvider();
        WaveFormat = sampleProvider.WaveFormat;

        var samples = new List<float>();
        var readBuffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
        int samplesRead;
        while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (var i = 0; i < samplesRead; i++)
            {
                samples.Add(readBuffer[i] * gain);
            }
        }

        AudioData = samples.ToArray();
    }
}

internal sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _cachedSound;
    private int _position;

    public CachedSoundSampleProvider(CachedSound cachedSound)
    {
        _cachedSound = cachedSound;
    }

    public WaveFormat WaveFormat => _cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = _cachedSound.AudioData.Length - _position;
        if (availableSamples <= 0)
        {
            return 0;
        }

        var samplesToCopy = Math.Min(availableSamples, count);
        Array.Copy(_cachedSound.AudioData, _position, buffer, offset, samplesToCopy);
        _position += samplesToCopy;
        return samplesToCopy;
    }
}

internal sealed class LoopingCachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _cachedSound;
    private int _position;

    public LoopingCachedSoundSampleProvider(CachedSound cachedSound)
    {
        _cachedSound = cachedSound;
    }

    public WaveFormat WaveFormat => _cachedSound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_cachedSound.AudioData.Length == 0)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        var written = 0;
        while (written < count)
        {
            var available = _cachedSound.AudioData.Length - _position;
            if (available <= 0)
            {
                _position = 0;
                continue;
            }

            var toCopy = Math.Min(available, count - written);
            Array.Copy(_cachedSound.AudioData, _position, buffer, offset + written, toCopy);
            _position += toCopy;
            written += toCopy;
        }

        return count;
    }
}
