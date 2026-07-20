using System;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Decorator that routes text generation to a primary provider and
    /// embeddings/attention to a secondary provider that actually has them.
    ///
    /// Anthropic and Gemini (as wired here) expose no embeddings endpoint, so on
    /// those providers every similarity-based feature — Ω convergence, χ selection,
    /// coherence, drift signals — degrades to lexical heuristics. Wrapping the
    /// primary with a real embeddings source (a local Ollama model, OpenAI, or any
    /// OpenAI-compatible server) restores the embedding tier without touching the
    /// generation path.
    /// </summary>
    public sealed class EmbeddingRoutingProvider : ILLMProvider, IDisposable
    {
        private readonly ILLMProvider _primary;
        private readonly ILLMProvider _embeddings;

        public EmbeddingRoutingProvider(ILLMProvider primary, ILLMProvider embeddings)
        {
            _primary    = primary ?? throw new ArgumentNullException(nameof(primary));
            _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        }

        public Task<string> GenerateAsync(
            string prompt,
            float temperature = 0.7f,
            CancellationToken cancellationToken = default)
            => _primary.GenerateAsync(prompt, temperature, cancellationToken);

        public Task<string> GenerateAsync(
            PromptPackage promptPackage,
            float temperature = 0.2f,
            CancellationToken cancellationToken = default)
            => _primary.GenerateAsync(promptPackage, temperature, cancellationToken);

        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => _embeddings.GetEmbeddingAsync(text, cancellationToken);

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => _embeddings.GetAttentionWeightsAsync(tokens, cancellationToken);

        public async Task<float[]?> GetLogProbsAsync(string text, CancellationToken cancellationToken = default)
            => await _primary.GetLogProbsAsync(text, cancellationToken)
               ?? await _embeddings.GetLogProbsAsync(text, cancellationToken);

        public void Dispose()
        {
            (_primary as IDisposable)?.Dispose();
            (_embeddings as IDisposable)?.Dispose();
        }
    }
}
