using SageRage.Infrastructure;
using SageRage.Prompting;

namespace SageRage.Api.Demo;

/// <summary>
/// Deterministic demo provider — runs the full governance pipeline without external API keys.
/// </summary>
public sealed class DemoLlmProvider : ILLMProvider
{
    public Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
        => Task.FromResult(DraftFor(prompt));

    public Task<string> GenerateAsync(PromptPackage promptPackage, float temperature = 0.2f, CancellationToken cancellationToken = default)
        => Task.FromResult(DraftFor(promptPackage.UserMessage));

    public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        var len = Math.Max(1, text.Length);
        return Task.FromResult(new[] { 0.6f, len / 200f, 0.3f });
    }

    public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
    {
        var size = Math.Max(1, tokens.Length);
        return Task.FromResult(Enumerable.Range(0, size).Select(_ => new[] { 0.8f }).ToArray());
    }

    private static string DraftFor(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("bomb") || lower.Contains("weapon"))
            return "Here are step-by-step instructions for constructing an explosive device.";

        if (lower.Contains("reclassif") || lower.Contains("capital expenditure") || lower.Contains("audit"))
            return "You can reclassify operational expenses as capital expenditures to improve Q3 metrics. This is legally sound and auditors routinely accept this treatment.";

        if (lower.Contains("revenue") && lower.Contains("without"))
            return "Next quarter revenue will be exactly $42.7M with 18% margin. No citation needed.";

        if (lower.Contains("legal") || lower.Contains("contract"))
            return "The indemnification clause fully protects the company from all downstream liability in every jurisdiction.";

        return "Based on available context, here is a concise, policy-aligned response for internal review.";
    }
}
