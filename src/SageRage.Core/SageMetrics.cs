using System;
using System.Linq;
using SageRage.Infrastructure;

namespace SageRage;

public class SageMetrics
{
    private readonly ILLMProvider _llm;

    public SageMetrics(ILLMProvider llm)
    {
        _llm = llm;
    }

    public async Task<float[]> GetEmbedding(string text, CancellationToken cancellationToken = default)
    {
        return await _llm.GetEmbeddingAsync(text, cancellationToken);
    }

    public float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length || vectorA.Length == 0)
            return 0f;

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            magnitudeA += vectorA[i] * vectorA[i];
            magnitudeB += vectorB[i] * vectorB[i];
        }

        magnitudeA = (float)Math.Sqrt(magnitudeA);
        magnitudeB = (float)Math.Sqrt(magnitudeB);

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Semantic similarity between two texts: embedding cosine when the provider
    /// has embeddings, lexical Jaccard overlap otherwise. Providers without an
    /// embeddings endpoint (Anthropic, Gemini) return empty vectors, and cosine
    /// on empty vectors is a dead 0 that silently disables every consumer —
    /// the fallback keeps convergence and selection alive on those providers.
    /// </summary>
    public async Task<float> TextSimilarity(string left, string right, CancellationToken cancellationToken = default)
    {
        var embA = await GetEmbedding(left, cancellationToken);
        var embB = await GetEmbedding(right, cancellationToken);

        if (embA.Length > 0 && embA.Length == embB.Length)
            return CosineSimilarity(embA, embB);

        return LexicalSimilarity(left, right);
    }

    /// <summary>Jaccard word-overlap similarity. Heuristic tier — order-insensitive.</summary>
    public static float LexicalSimilarity(string left, string right)
    {
        var leftWords = left.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightWords = right.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (leftWords.Count == 0 && rightWords.Count == 0)
            return 1f;

        var intersection = leftWords.Intersect(rightWords, StringComparer.OrdinalIgnoreCase).Count();
        var union = leftWords.Union(rightWords, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0f : intersection / (float)union;
    }

    /// <summary>
    /// Two-tier estimate per whitepaper §13: model log-probabilities when the
    /// provider exposes them (true perplexity), heuristic uncertainty scoring
    /// otherwise. Never returns a sentinel as a measurement.
    /// </summary>
    public async Task<float> CalculatePerplexity(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 1f;

        var logProbs = await _llm.GetLogProbsAsync(text);
        if (logProbs is { Length: > 0 })
            return (float)Math.Exp(-logProbs.Average());

        return HeuristicPerplexity(words);
    }

    /// <summary>
    /// Heuristic tier: perplexity of the text's own unigram distribution (2^H).
    /// Repetitive text scores near 1, lexically diverse text scores higher.
    /// This measures lexical spread, not model uncertainty — a labeled proxy,
    /// not a substitute for log-probabilities.
    /// </summary>
    public static float HeuristicPerplexity(string[] words)
    {
        if (words.Length == 0) return 1f;

        var counts = words
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .Select(g => (double)g.Count())
            .ToArray();
        double total = counts.Sum();

        double entropy = 0.0;
        foreach (var count in counts)
        {
            var p = count / total;
            entropy -= p * Math.Log2(p);
        }

        return (float)Math.Pow(2, entropy);
    }

    /// <summary>
    /// Gini coefficient of the flattened attention distribution.
    /// G = Σ (2i − n − 1)·x_i / (n·Σx) over ascending-sorted x, i 1-indexed.
    /// Returns 0 for uniform weights, →1 as mass concentrates.
    /// </summary>
    public float GiniCoefficient(float[][] attentionWeights)
    {
        if (attentionWeights == null || attentionWeights.Length == 0)
            return 0f;

        float[] allWeights = attentionWeights.SelectMany(row => row).ToArray();
        if (allWeights.Length == 0) return 0f;

        Array.Sort(allWeights);

        int n = allWeights.Length;
        double totalSum = 0.0;
        double weightedSum = 0.0;

        for (int i = 0; i < n; i++)
        {
            totalSum += allWeights[i];
            weightedSum += (2.0 * (i + 1) - n - 1) * allWeights[i];
        }

        if (totalSum <= 0.0) return 0f;

        return (float)(weightedSum / (n * totalSum));
    }
}
