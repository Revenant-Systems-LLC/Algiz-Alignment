using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// REST provider for Gemini text models via the generateContent endpoint.
    /// Use <see cref="PrimaryModel"/> / <see cref="FallbackModel"/> for cloud text.
    /// For Live Audio, see <see cref="GeminiLiveAudioProvider"/>.
    /// </summary>
    public sealed class GeminiLLMProvider : ILLMProvider, IDisposable
    {
        // ── REST text models (generateContent) ──────────────────────────────
        public const string PrimaryModel  = "gemini-2.0-flash";
        public const string FallbackModel = "gemini-2.0-flash-lite";

        private static readonly HashSet<string> AllowedModels = new(StringComparer.OrdinalIgnoreCase)
        {
            PrimaryModel,
            FallbackModel
        };

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly string     _apiKey;
        private readonly string     _preferredModel;

        /// <param name="geminiLiveApiKey">Optional explicit key; falls back to GEMINI_API_KEY env var.</param>
        /// <param name="model">Must be <see cref="PrimaryModel"/> or <see cref="FallbackModel"/>; defaults to PrimaryModel.</param>
        public GeminiLLMProvider(
            string?     geminiLiveApiKey = null,
            HttpClient? httpClient       = null,
            string?     model            = null)
        {
            var resolvedKey = string.IsNullOrWhiteSpace(geminiLiveApiKey)
                ? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                : geminiLiveApiKey;

            if (string.IsNullOrWhiteSpace(resolvedKey))
                throw new InvalidOperationException(
                    "Missing Gemini API key. Provide it directly or set GEMINI_API_KEY.");

            _apiKey         = resolvedKey;
            _ownsHttpClient = httpClient is null;
            _httpClient     = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(45);
            var resolved = string.IsNullOrWhiteSpace(model) ? PrimaryModel : model;
            if (!AllowedModels.Contains(resolved))
                throw new InvalidOperationException(
                    $"Model '{resolved}' is not in the allowed list. Use PrimaryModel or FallbackModel.");
            _preferredModel = resolved;
        }

        public Task<string> GenerateAsync(
            string prompt,
            float temperature             = 0.7f,
            CancellationToken cancellationToken = default)
        {
            var package = new PromptPackage(
                "You are a concise, helpful assistant.",
                prompt,
                Array.Empty<ContextItem>());
            return GenerateAsync(package, temperature, cancellationToken);
        }

        public async Task<string> GenerateAsync(
            PromptPackage promptPackage,
            float temperature             = 0.2f,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                system_instruction = new { parts = new[] { new { text = promptPackage.SystemInstruction } } },
                contents = new[]
                {
                    new
                    {
                        role  = "user",
                        parts = new[] { new { text = PromptHelpers.BuildUserPayload(promptPackage) } }
                    }
                },
                generationConfig = new { temperature }
            };

            // Try preferred model first, then fallback
            var models    = _preferredModel == FallbackModel
                ? new[] { FallbackModel, PrimaryModel }
                : new[] { _preferredModel, FallbackModel };

            Exception? lastError = null;

            foreach (var model in models)
            {
                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post, BuildEndpoint(model))
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                    };

                    request.Headers.Add("x-goog-api-key", _apiKey);

                    try
                    {
                        using var response = await _httpClient.SendAsync(
                            request, cancellationToken);
                        var body = await response.Content.ReadAsStringAsync(cancellationToken);

                        if (IsModelIssue(response.StatusCode))
                        {
                            lastError = new InvalidOperationException(
                                $"Model '{model}' unavailable ({(int)response.StatusCode}).");
                            break;
                        }

                        if (IsTransient(response.StatusCode) && attempt < 3)
                        {
                            await Task.Delay(
                                TimeSpan.FromMilliseconds(250 * attempt * attempt),
                                cancellationToken);
                            continue;
                        }

                        response.EnsureSuccessStatusCode();

                        using var doc = JsonDocument.Parse(body);
                        return doc.RootElement
                            .GetProperty("candidates")[0]
                            .GetProperty("content")
                            .GetProperty("parts")[0]
                            .GetProperty("text")
                            .GetString() ?? string.Empty;
                    }
                    catch (HttpRequestException ex) when (attempt < 3)
                    {
                        lastError = ex;
                        await Task.Delay(
                            TimeSpan.FromMilliseconds(250 * attempt * attempt),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        break;
                    }
                }
            }

            throw new InvalidOperationException(
                "Gemini request failed after retries and fallback.", lastError);
        }

        /// <summary>
        /// Not wired to Gemini's embedding endpoint yet. Returns empty to signal
        /// "unavailable"; pair with an embeddings source via
        /// <see cref="EmbeddingRoutingProvider"/>. Consumers fall back to lexical
        /// heuristics when empty.
        /// </summary>
        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<float>());

        /// <summary>
        /// Attention weights are not exposed by any commercial API. Returns empty to
        /// signal "unavailable" rather than fabricating a uniform matrix — consumers
        /// must skip attention-based metrics instead of measuring fiction.
        /// </summary>
        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<float[]>());

        private string BuildEndpoint(string model)
            => $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

        private static bool IsTransient(HttpStatusCode s)
            => s is HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.ServiceUnavailable;

        private static bool IsModelIssue(HttpStatusCode s)
            => s is HttpStatusCode.NotFound or HttpStatusCode.BadRequest;

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _httpClient.Dispose();
            }
        }
    }
}
