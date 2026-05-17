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
    /// Provider for OpenAI models via the Chat Completions API.
    /// Requires OPENAI_API_KEY environment variable or explicit key.
    /// For local OpenAI-compatible servers (LM Studio, vLLM, etc.),
    /// use <see cref="OpenAICompatibleProvider"/> instead.
    /// </summary>
    public sealed class OpenAIProvider : ILLMProvider, IDisposable
    {
        public const string PrimaryModel   = "gpt-4o";
        public const string FallbackModel  = "gpt-4o-mini";
        public const string EmbeddingModel = "text-embedding-3-small";

        private const string BaseUrl = "https://api.openai.com/v1";

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly string     _apiKey;
        private readonly string     _preferredModel;

        /// <param name="apiKey">Optional explicit key; falls back to OPENAI_API_KEY env var.</param>
        /// <param name="model">Any OpenAI model name; defaults to <see cref="PrimaryModel"/>.</param>
        public OpenAIProvider(
            string?     apiKey     = null,
            HttpClient? httpClient = null,
            string?     model      = null)
        {
            var resolvedKey = string.IsNullOrWhiteSpace(apiKey)
                ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                : apiKey;

            if (string.IsNullOrWhiteSpace(resolvedKey))
                throw new InvalidOperationException(
                    "Missing OpenAI API key. Provide it directly or set OPENAI_API_KEY.");

            _apiKey         = resolvedKey;
            _ownsHttpClient = httpClient is null;
            _httpClient     = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            _preferredModel = string.IsNullOrWhiteSpace(model) ? PrimaryModel : model;
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
            // Try preferred model first, then fallback
            var models = _preferredModel == FallbackModel
                ? new[] { FallbackModel, PrimaryModel }
                : new[] { _preferredModel, FallbackModel };

            Exception? lastError = null;

            foreach (var model in models)
            {
                var payload = new
                {
                    model,
                    messages = new object[]
                    {
                        new { role = "system", content = promptPackage.SystemInstruction },
                        new { role = "user",   content = PromptHelpers.BuildUserPayload(promptPackage) }
                    },
                    temperature,
                    max_tokens = 4096
                };

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post, $"{BaseUrl}/chat/completions")
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(payload),
                            Encoding.UTF8,
                            "application/json")
                    };

                    request.Headers.Add("Authorization", $"Bearer {_apiKey}");

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
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
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
                "OpenAI request failed after retries and fallback.", lastError);
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var payload = new { model = EmbeddingModel, input = text };

            using var request = new HttpRequestMessage(
                HttpMethod.Post, $"{BaseUrl}/embeddings")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Add("Authorization", $"Bearer {_apiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding")
                .EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
        }

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
        {
            // OpenAI does not expose raw attention weights.
            // Uniform approximation consistent with other providers.
            var w = new float[tokens.Length][];
            for (var i = 0; i < tokens.Length; i++)
                w[i] = Enumerable.Repeat(1f / Math.Max(1, tokens.Length), tokens.Length).ToArray();
            return Task.FromResult(w);
        }

        private static bool IsTransient(HttpStatusCode s)
            => s is HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.InternalServerError;

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
