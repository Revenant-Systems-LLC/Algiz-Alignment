using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Connects to the Gemini Live API over WebSocket for native audio I/O.
    /// These models are ONLY valid on the Live WebSocket endpoint —
    /// NOT on the REST generateContent endpoint.
    /// </summary>
    public sealed class GeminiLiveAudioProvider : ILiveAudioProvider
    {
        // ── Live API (WebSocket) audio models ─────────────────────────────
        public const string LiveModel         = "gemini-2.5-flash-native-audio-preview-12-2025";
        public const string LiveFallbackModel = "gemini-2.5-flash-native-audio-preview-09-2025";

        private readonly string _apiKey;

        public GeminiLiveAudioProvider(string? apiKey = null)
        {
            var key = string.IsNullOrWhiteSpace(apiKey)
                ? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                : apiKey;

            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException(
                    "Missing Gemini API key. Set GEMINI_API_KEY environment variable.");

            _apiKey = key;
        }

        public async Task<ILiveAudioSession> ConnectAsync(
            string model, LiveConfig config, CancellationToken ct = default)
        {
            var ws  = new ClientWebSocket();
            ws.Options.SetRequestHeader("x-goog-api-key", _apiKey);
            var uri = new Uri(
                "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha" +
                ".GenerativeService.BidiGenerateContent");

            await ws.ConnectAsync(uri, ct);

            var session = new GeminiLiveSession(ws);
            await session.InitAsync(model, config, ct);
            return session;
        }
    }

    internal sealed class GeminiLiveSession : ILiveAudioSession
    {
        private readonly ClientWebSocket _ws;
        private const int BufferSize = 64 * 1024;

        internal GeminiLiveSession(ClientWebSocket ws) => _ws = ws;

        internal async Task InitAsync(string model, LiveConfig config, CancellationToken ct)
        {
            var modelPath = model.StartsWith("models/", StringComparison.Ordinal)
                ? model
                : $"models/{model}";

            var setup = new
            {
                setup = new
                {
                    model = modelPath,
                    generation_config = new
                    {
                        response_modalities = config.ResponseModalities,
                        speech_config = new
                        {
                            voice_config = new
                            {
                                prebuilt_voice_config = new { voice_name = "Aoede" }
                            }
                        }
                    },
                    system_instruction = new
                    {
                        parts = new[] { new { text = config.SystemInstruction } }
                    },
                    input_audio_transcription  = new { },
                    output_audio_transcription = new { }
                }
            };

            await SendJsonAsync(setup, ct);
        }

        public async Task SendAudioAsync(byte[] pcm16kChunk, CancellationToken ct = default)
        {
            var msg = new
            {
                realtime_input = new
                {
                    media_chunks = new[]
                    {
                        new { mime_type = "audio/pcm", data = Convert.ToBase64String(pcm16kChunk) }
                    }
                }
            };
            await SendJsonAsync(msg, ct);
        }

        public async Task SendTextAsync(
            string text, bool endOfTurn = true, CancellationToken ct = default)
        {
            var msg = new
            {
                client_content = new
                {
                    turns = new[] { new { role = "user", parts = new[] { new { text } } } },
                    turn_complete = endOfTurn
                }
            };
            await SendJsonAsync(msg, ct);
        }

        public async IAsyncEnumerable<LiveEvent> ReceiveAsync(
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var buffer = new byte[BufferSize];

            while (_ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                string? json      = null;
                bool    terminate = false;

                try
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        terminate = true;
                    else
                        json = Encoding.UTF8.GetString(ms.ToArray());
                }
                catch (OperationCanceledException) { terminate = true; }
                catch (WebSocketException)         { terminate = true; }
                catch (JsonException)               { /* skip malformed JSON frame */ }
                catch (FormatException)            { /* skip malformed frame */ }

                if (terminate) yield break;
                if (json is null) continue;

                List<LiveEvent> events;
                try   { events = ParseEvents(json); }
                catch { continue; }

                foreach (var evt in events)
                    yield return evt;
            }
        }

        private static List<LiveEvent> ParseEvents(string json)
        {
            var result = new List<LiveEvent>();
            using var doc  = JsonDocument.Parse(json);
            var       root = doc.RootElement;

            if (root.TryGetProperty("setupComplete", out _))
            {
                result.Add(new SetupComplete());
                return result;
            }

            if (!root.TryGetProperty("serverContent", out var sc)) return result;

            if (sc.TryGetProperty("inputTranscription", out var it) &&
                it.TryGetProperty("text", out var itTxt))
                result.Add(new InputTranscription(itTxt.GetString() ?? ""));

            if (sc.TryGetProperty("outputTranscription", out var ot) &&
                ot.TryGetProperty("text", out var otTxt))
                result.Add(new OutputTranscription(otTxt.GetString() ?? ""));

            if (sc.TryGetProperty("modelTurn", out var mt) &&
                mt.TryGetProperty("parts", out var parts))
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.TryGetProperty("inlineData", out var inline) &&
                        inline.TryGetProperty("data", out var b64))
                    {
                        result.Add(new OutputAudioChunk(
                            Convert.FromBase64String(b64.GetString()!)));
                    }
                    else if (part.TryGetProperty("text", out var tp))
                    {
                        result.Add(new OutputTranscription(tp.GetString() ?? ""));
                    }
                }
            }

            if (sc.TryGetProperty("turnComplete", out var tc) &&
                tc.ValueKind == JsonValueKind.True)
                result.Add(new TurnComplete());

            return result;
        }

        private async Task SendJsonAsync(object payload, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (_ws.State == WebSocketState.Open)
                await _ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None);
            _ws.Dispose();
        }
    }
}
