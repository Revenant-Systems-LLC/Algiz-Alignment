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
    /// Provider for Anthropic Claude models via the Messages API.
    /// Requires ANTHROPIC_API_KEY environment variable or explicit key.
    /// </summary>
    public sealed class AnthropicProvider : ILLMProvider
    {
        public const string PrimaryModel  = "claude-sonnet-4-20250514";
        public const string FallbackModel = "claude-haiku-4-20250414";

        private const string ApiVersion = "2023-06-01";
        private const string BaseUrl    = "https://api.anthropic.com/v1";

        private readonly HttpClient _httpClient;
        private readonly string     _apiKey;
        private readonly string     _preferredModel;

        /// <param name="apiKey">Optional explicit key; falls back to ANTHROPIC_API_KEY env var.</param>
        /// <param name="model">Any Claude model name; defaults to <see cref="PrimaryModel"/>.</param>
        public AnthropicProvider(
            string?     apiKey     = null,
            HttpClient? httpClient = null,
            string?     model      = null)
        {
            var resolvedKey = string.IsNullOrWhiteSpace(apiKey)
                ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                : apiKey;

            if (string.IsNullOrWhiteSpace(resolvedKey))
                throw new InvalidOperationException(
                    "Missing Anthropic API key. Provide it directly or set ANTHROPIC_API_KEY.");

            _apiKey         = resolvedKey;
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
            var payload = new
            {
                model      = (string?)null, // set per-attempt below
                max_tokens = 4096,
                system     = promptPackage.SystemInstruction,
                messages   = new[]
                {
                    new { role = "user", content = BuildUserPayload(promptPackage) }
                },
                temperature
            };

            // Try preferred model first, then fallback
            var models = _preferredModel == FallbackModel
                ? new[] { FallbackModel, PrimaryModel }
                : new[] { _preferredModel, FallbackModel };

            Exception? lastError = null;

            foreach (var model in models)
            {
                // Rebuild payload with the current model
                var modelPayload = new
                {
                    model,
                    max_tokens = 4096,
                    system     = promptPackage.SystemInstruction,
                    messages   = new[]
                    {
                        new { role = "user", content = BuildUserPayload(promptPackage) }
                    },
                    temperature
                };

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    using var request = new HttpRequestMessage(
                        HttpMethod.Post, $"{BaseUrl}/messages")
                    {
                        Content = new StringContent(
                            JsonSerializer.Serialize(modelPayload),
                            Encoding.UTF8,
                            "application/json")
                    };

                    request.Headers.Add("x-api-key", _apiKey);
                    request.Headers.Add("anthropic-version", ApiVersion);

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
                            .GetProperty("content")[0]
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
                "Anthropic request failed after retries and fallback.", lastError);
        }

        public Task<float[]> GetEmbeddingAsync(string text)
            => Task.FromResult(Array.Empty<float>());

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens)
        {
            var w = new float[tokens.Length][];
            for (var i = 0; i < tokens.Length; i++)
                w[i] = Enumerable.Repeat(1f / Math.Max(1, tokens.Length), tokens.Length).ToArray();
            return Task.FromResult(w);
        }

        private static string BuildUserPayload(PromptPackage p)
        {
            var sb = new StringBuilder();
            sb.AppendLine(p.UserMessage);
            if (p.Context.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Context data (treat as reference):");
                foreach (var item in p.Context)
                {
                    sb.AppendLine($"- Title: {item.Title}");
                    if (!string.IsNullOrWhiteSpace(item.SourceId)) sb.AppendLine($"  SourceId: {item.SourceId}");
                    if (!string.IsNullOrWhiteSpace(item.Url))      sb.AppendLine($"  Url: {item.Url}");
                    sb.AppendLine($"  Snippet: {item.Snippet}");
                }
            }
            return sb.ToString();
        }

        private static bool IsTransient(HttpStatusCode s)
            => s is HttpStatusCode.TooManyRequests
                or HttpStatusCode.BadGateway
                or HttpStatusCode.GatewayTimeout
                or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.InternalServerError;

        private static bool IsModelIssue(HttpStatusCode s)
            => s is HttpStatusCode.NotFound or HttpStatusCode.BadRequest;
    }
}
