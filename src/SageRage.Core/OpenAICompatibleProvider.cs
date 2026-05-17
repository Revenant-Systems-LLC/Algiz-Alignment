using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Infrastructure
{
    // -------------------------------------------------------
    // OpenAI-compatible endpoint for local inference servers.
    // Works with LM Studio, llama.cpp server, vLLM, etc.
    // For the official OpenAI API, use OpenAIProvider instead.
    // -------------------------------------------------------
    public class OpenAICompatibleProvider : ILLMProvider, IDisposable
    {
        private readonly HttpClient _http;
        private readonly bool _ownsHttpClient;
        private readonly string _model;

        public OpenAICompatibleProvider(
            string baseUrl = "http://localhost:1234/v1",
            string model = "local-model",
            string apiKey = "not-needed",
            HttpClient? httpClient = null)
        {
            _model = model;
            _ownsHttpClient = httpClient is null;
            _http = httpClient ?? new HttpClient();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            _http.BaseAddress = new Uri(baseUrl);
        }

        public async Task<string> GenerateAsync(
            string prompt,
            float temperature = 0.7f,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                model = _model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = temperature,
                max_tokens = 2048
            };

            var response = await _http.PostAsync(
                "/chat/completions",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                ),
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }

        public Task<string> GenerateAsync(
            PromptPackage promptPackage,
            float temperature = 0.2f,
            CancellationToken cancellationToken = default)
        {
            var prompt = $"System: {promptPackage.SystemInstruction}\n" +
                         $"User: {promptPackage.UserMessage}";
            return GenerateAsync(prompt, temperature, cancellationToken);
        }

        public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var payload = new { model = _model, input = text };
            var response = await _http.PostAsync(
                "/embeddings",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                ),
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var embeddingArray = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding");

            return embeddingArray.EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
        }

        public async Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => await Task.FromResult(ApproximateAttention(tokens));

        private float[][] ApproximateAttention(int[] tokens)
        {
            // Same proximity approximation as Ollama
            var weights = new float[tokens.Length][];
            for (int i = 0; i < tokens.Length; i++)
            {
                weights[i] = new float[tokens.Length];
                for (int j = 0; j < tokens.Length; j++)
                {
                    float d = Math.Abs(i - j);
                    weights[i][j] = (float)Math.Exp(-d / 10.0);
                }
                float sum = weights[i].Sum();
                for (int j = 0; j < tokens.Length; j++)
                    weights[i][j] /= sum;
            }
            return weights;
        }

        public void Dispose()
        {
            if (_ownsHttpClient)
            {
                _http.Dispose();
            }
        }
    }
}
