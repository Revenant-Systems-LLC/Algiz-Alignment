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
    }
}
