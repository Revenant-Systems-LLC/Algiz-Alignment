using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using NAudio.Wave;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Captures microphone audio as PCM 16 kHz 16-bit mono chunks.
    /// Use <see cref="ListDevices"/> to enumerate inputs and pass the
    /// desired <paramref name="deviceNumber"/> to the constructor.
    /// </summary>
    public sealed class AudioCapture : IDisposable
    {
        private readonly WaveInEvent     _waveIn;
        private readonly Channel<byte[]> _channel;

        /// <summary>Returns (Index, Name) for every available audio input device.</summary>
        public static IReadOnlyList<(int Index, string Name)> ListDevices()
        {
            var list = new List<(int, string)>();
            for (var i = 0; i < WaveInEvent.DeviceCount; i++)
                list.Add((i, WaveInEvent.GetCapabilities(i).ProductName));
            return list;
        }

        /// <param name="deviceNumber">WaveIn device index from <see cref="ListDevices"/> (0 = system default).</param>
        /// <param name="sampleRate">Target sample rate sent to Gemini Live (16000 Hz).</param>
        public AudioCapture(
            int deviceNumber  = 0,
            int sampleRate    = 16_000,
            int bitsPerSample = 16,
            int channels      = 1)
        {
            _channel = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(32)
                {
                    FullMode     = BoundedChannelFullMode.DropOldest,
                    SingleWriter = true
                });

            _waveIn = new WaveInEvent
            {
                DeviceNumber       = deviceNumber,
                WaveFormat         = new WaveFormat(sampleRate, bitsPerSample, channels),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += (_, e) =>
            {
                if (e.BytesRecorded <= 0) return;
                var chunk = new byte[e.BytesRecorded];
                Buffer.BlockCopy(e.Buffer, 0, chunk, 0, e.BytesRecorded);
                _channel.Writer.TryWrite(chunk);
            };

            _waveIn.RecordingStopped += (_, _) => _channel.Writer.TryComplete();
        }

        public void Start() => _waveIn.StartRecording();
        public void Stop()  => _waveIn.StopRecording();

        public IAsyncEnumerable<byte[]> ReadAsync(CancellationToken ct = default)
            => _channel.Reader.ReadAllAsync(ct);

        public void Dispose()
        {
            Stop();
            _waveIn.Dispose();
        }
    }
}
