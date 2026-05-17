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

    public async Task<float> CalculatePerplexity(string text)
    {
        // This is a simplified perplexity estimation
        // In a real implementation, this would use model log probabilities
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return float.MaxValue;

        // Use embedding variance as a proxy for perplexity
        var embedding = await GetEmbedding(text);
        if (embedding.Length == 0) return float.MaxValue;

        var mean = embedding.Average();
        var variance = embedding.Sum(x => Math.Pow(x - mean, 2)) / embedding.Length;
        return (float)Math.Sqrt(variance);
    }

    public float GiniCoefficient(float[][] attentionWeights)
    {
        if (attentionWeights == null || attentionWeights.Length == 0)
            return 0f;

        float[] allWeights = attentionWeights.SelectMany(row => row).ToArray();
        if (allWeights.Length == 0) return 0f;

        Array.Sort(allWeights);
        
        float cumulativeSum = 0f;
        float giniSum = 0f;
        
        for (int i = 0; i < allWeights.Length; i++)
        {
            cumulativeSum += allWeights[i];
            giniSum += cumulativeSum;
        }

        float mean = cumulativeSum / allWeights.Length;
        if (mean == 0) return 0f;

        float gini = (2 * giniSum) / (allWeights.Length * cumulativeSum) - (allWeights.Length + 1) / (float)allWeights.Length;
        return Math.Max(0f, gini);
    }
}