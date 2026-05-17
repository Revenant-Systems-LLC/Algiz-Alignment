using System;
using NAudio.Wave;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Plays PCM 24 kHz 16-bit mono audio (Gemini Live native output format).
    /// Queue chunks as they arrive; playback is continuous and low-latency.
    /// </summary>
    public sealed class AudioPlayback : IDisposable
    {
        private readonly WaveOutEvent         _waveOut;
        private readonly BufferedWaveProvider _buffer;

        public AudioPlayback(
            int sampleRate    = 24_000,
            int bitsPerSample = 16,
            int channels      = 1)
        {
            _buffer = new BufferedWaveProvider(
                new WaveFormat(sampleRate, bitsPerSample, channels))
            {
                BufferDuration          = TimeSpan.FromSeconds(30),
                DiscardOnBufferOverflow = true
            };

            _waveOut = new WaveOutEvent { DesiredLatency = 150 };
            _waveOut.Init(_buffer);
            _waveOut.Play();
        }

        /// <summary>Enqueue a PCM chunk for immediate playback.</summary>
        public void QueueAudio(byte[] pcmChunk) =>
            _buffer.AddSamples(pcmChunk, 0, pcmChunk.Length);

        public void Dispose()
        {
            _waveOut.Stop();
            _waveOut.Dispose();
        }
    }
}
