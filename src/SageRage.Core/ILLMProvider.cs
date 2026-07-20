// SageRage/Infrastructure/ILLMProvider.cs
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

        Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);
        Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default);

        /// <summary>
        /// Per-token log-probabilities for <paramref name="text"/>, or null when the
        /// provider does not expose them (Anthropic withholds logprobs entirely; most
        /// commercial chat APIs cannot score arbitrary text). Callers must treat null
        /// as "unsupported" and fall back to heuristic scoring per whitepaper §13.
        /// </summary>
        Task<float[]?> GetLogProbsAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<float[]?>(null);
    }
}
