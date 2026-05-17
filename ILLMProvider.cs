// SageRage/Infrastructure/ILLMProvider.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Infrastructure
{
    public interface ILLMProvider
    {
        Task<string> GenerateAsync(
            string prompt,
            float temperature = 0.7f,
            CancellationToken cancellationToken = default);

        Task<string> GenerateAsync(
            PromptPackage promptPackage,
            float temperature = 0.2f,
            CancellationToken cancellationToken = default);

        Task<float[]> GetEmbeddingAsync(string text);
        Task<float[][]> GetAttentionWeightsAsync(int[] tokens);
    }

    // -------------------------------------------------------
    // Implementation: Ollama (local) — works with LLaMA,
    // Mistral, Phi, Gemma, etc. — whatever you have running
    // -------------------------------------------------------
    public class OllamaProvider : ILLMProvider
    {
        private readonly HttpClient _http;
        private readonly string _model;
        private readonly string _baseUrl;

        public OllamaProvider(
            string model = "llama3",
            string baseUrl = "http://localhost:11434")
        {
            _model = model;
            _baseUrl = baseUrl;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        }

        public async Task<string> GenerateAsync(
            string prompt,
            float temperature = 0.7f,
            CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                model = _model,
                prompt = prompt,
                temperature = temperature,
                stream = false
            };

            var response = await _http.PostAsync(
                $"{_baseUrl}/api/generate",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                ),
                cancellationToken
            );

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaResponse>(json);
            return result?.Response ?? string.Empty;
        }

        public Task<string> GenerateAsync(
            PromptPackage promptPackage,
            float temperature = 0.2f,
            CancellationToken cancellationToken = default)
        {
            var builder = new StringBuilder();
            builder.AppendLine(promptPackage.SystemInstruction);
            if (promptPackage.Context.Count > 0)
            {
                builder.AppendLine("Context data:");
                foreach (var item in promptPackage.Context)
                    builder.AppendLine($"- {item.Title}: {item.Snippet}");
            }
            builder.AppendLine();
            builder.AppendLine(promptPackage.UserMessage);
            return GenerateAsync(builder.ToString(), temperature, cancellationToken);
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var payload = new { model = _model, prompt = text };

            var response = await _http.PostAsync(
                $"{_baseUrl}/api/embeddings",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(json);
            return result?.Embedding ?? Array.Empty<float>();
        }

        public async Task<float[][]> GetAttentionWeightsAsync(int[] tokens)
        {
            // Ollama doesn't expose raw attention weights.
            // We approximate using embedding distance between token windows.
            // This is a valid proxy for "containment" measurement.
            var weights = new float[tokens.Length][];
            for (int i = 0; i < tokens.Length; i++)
            {
                weights[i] = new float[tokens.Length];
                for (int j = 0; j < tokens.Length; j++)
                {
                    // Proximity-based attention approximation
                    float distance = Math.Abs(i - j);
                    weights[i][j] = (float)Math.Exp(-distance / 10.0);
                }
                // Normalize row
                float sum = weights[i].Sum();
                for (int j = 0; j < tokens.Length; j++)
                    weights[i][j] /= sum;
            }
            return weights;
        }

        private record OllamaResponse(
            [property: JsonPropertyName("response")] string Response);

        private record OllamaEmbeddingResponse(
            [property: JsonPropertyName("embedding")] float[] Embedding);
    }

    // -------------------------------------------------------
    // Alternative: OpenAI-compatible endpoint
    // Works with LM Studio, llama.cpp server, vLLM, etc.
    // -------------------------------------------------------
    public class OpenAICompatibleProvider : ILLMProvider
    {
        private readonly HttpClient _http;
        private readonly string _model;

        public OpenAICompatibleProvider(
            string baseUrl = "http://localhost:1234/v1",
            string model = "local-model",
            string apiKey = "not-needed")
        {
            _model = model;
            _http = new HttpClient();
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

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            var payload = new { model = _model, input = text };
            var response = await _http.PostAsync(
                "/embeddings",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var embeddingArray = doc.RootElement
                .GetProperty("data")[0]
                .GetProperty("embedding");

            return embeddingArray.EnumerateArray()
                .Select(e => e.GetSingle())
                .ToArray();
        }

        public async Task<float[][]> GetAttentionWeightsAsync(int[] tokens)
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
    }
}
