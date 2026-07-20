using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Infrastructure;
using SageRage.Prompting;
using Xunit;

namespace SageRage.Tests;

/// <summary>
/// Regression tests for FINDINGS-2026-07-17. Each test pins a defect that
/// previously made a metric constant, a selection dead, or a measurement
/// fabricated: Gini sign-inversion (§5), perplexity-as-sentinel (§4),
/// Ω non-convergence (§2), χ non-selection (§3), fabricated attention (§1),
/// and the lexicon-only sentiment engine (§6).
/// </summary>
public class FindingsRegressionTests
{
    // ── §5 GiniCoefficient ──────────────────────────────────────────

    [Fact]
    public void Gini_MaxInequality_Returns075()
    {
        var metrics = new SageMetrics(new NoEmbeddingProvider());
        var gini = metrics.GiniCoefficient(new[] { new[] { 0f, 0f, 0f, 4f } });
        Assert.Equal(0.75f, gini, 3);
    }

    [Fact]
    public void Gini_ModerateInequality_Returns0375()
    {
        var metrics = new SageMetrics(new NoEmbeddingProvider());
        var gini = metrics.GiniCoefficient(new[] { new[] { 1f, 1f, 1f, 5f } });
        Assert.Equal(0.375f, gini, 3);
    }

    [Fact]
    public void Gini_PerfectEquality_ReturnsZero()
    {
        var metrics = new SageMetrics(new NoEmbeddingProvider());
        var gini = metrics.GiniCoefficient(new[] { new[] { 1f, 1f, 1f, 1f } });
        Assert.Equal(0f, gini, 3);
    }

    // ── §4 CalculatePerplexity ──────────────────────────────────────

    [Fact]
    public void HeuristicPerplexity_RepetitiveTextScoresLow_DiverseScoresHigh()
    {
        var repetitive = SageMetrics.HeuristicPerplexity(new[] { "a", "a", "a", "a" });
        var diverse    = SageMetrics.HeuristicPerplexity(new[] { "one", "two", "three", "four" });

        Assert.Equal(1f, repetitive, 3);
        Assert.Equal(4f, diverse, 3);
    }

    [Fact]
    public async Task CalculatePerplexity_IsNotConstant_WithoutEmbeddingsOrLogProbs()
    {
        var metrics = new SageMetrics(new NoEmbeddingProvider());

        var repetitive = await metrics.CalculatePerplexity("the the the the");
        var diverse    = await metrics.CalculatePerplexity("alpha beta gamma delta");

        Assert.True(repetitive < diverse,
            $"Expected repetitive ({repetitive}) < diverse ({diverse}) — a constant function means the §13 heuristic tier is dead.");
        Assert.True(diverse < float.MaxValue, "Sentinel float.MaxValue must never be passed downstream as a measurement.");
    }

    [Fact]
    public async Task CalculatePerplexity_UsesLogProbs_WhenProviderExposesThem()
    {
        var metrics = new SageMetrics(new LogProbProvider(new[] { -1f, -1f, -1f }));

        var perplexity = await metrics.CalculatePerplexity("some text here");

        Assert.Equal((float)Math.E, perplexity, 2);
    }

    // ── §1/§2 TextSimilarity fallback and Ω convergence ─────────────

    [Fact]
    public async Task TextSimilarity_FallsBackToLexical_WhenNoEmbeddings()
    {
        var metrics = new SageMetrics(new NoEmbeddingProvider());

        Assert.Equal(1f, await metrics.TextSimilarity("same words here", "same words here"), 3);
        Assert.Equal(0f, await metrics.TextSimilarity("completely different", "unrelated tokens"), 3);
    }

    [Fact]
    public async Task Omega_Converges_OnProvidersWithoutEmbeddings()
    {
        using var runtime = new SageRuntime(new NoEmbeddingProvider());

        var result = await runtime.ExecuteOmega("input question");

        Assert.True(string.IsNullOrEmpty(result.Warning),
            $"Ω reported non-convergence: '{result.Warning}'");
        Assert.True((float)result.Metadata["convergence_score"] > 0.9f,
            "Ω must reach its fixed point when reflections stabilize — similarity 0-forever means the convergence check is decorative.");
    }

    // ── §3 χ selection ──────────────────────────────────────────────

    [Fact]
    public async Task Chi_SelectsMostCoherentCandidate_NotAlwaysFirst()
    {
        var input = "governance safety runtime wrapper";
        var provider = new TemperatureKeyedProvider(temp => temp switch
        {
            < 0.4f => "zebra quantum pickle mismatch junk",
            < 0.6f => input, // echo — maximal coherence with the input
            _      => "unrelated words here"
        });
        using var runtime = new SageRuntime(provider);

        var result = await runtime.ExecuteChi(input);

        Assert.Equal(input, result.Output);
        Assert.Equal(0.5f, (float)result.Metadata["selected_temperature"], 2);
        Assert.Equal(1f, (float)result.Metadata["coherence_score"], 2);
    }

    [Fact]
    public async Task Chi_EmptyCandidate_NeverWinsSelection()
    {
        var provider = new TemperatureKeyedProvider(temp =>
            temp < 0.4f ? string.Empty : "a real answer");
        using var runtime = new SageRuntime(provider);

        var result = await runtime.ExecuteChi("any input");

        Assert.Equal("a real answer", result.Output);
    }

    // ── §1/§6 attention honesty ─────────────────────────────────────

    [Fact]
    public async Task Containment_ReportsAttentionUnavailable_InsteadOfFabricatedZero()
    {
        using var runtime = new SageRuntime(new NoEmbeddingProvider());

        var result = await runtime.ExecuteContainment("input text");

        Assert.False((bool)result.Metadata["attention_available"]);
        Assert.False((bool)result.Metadata["isolation_verified"]);
        Assert.False(result.Metadata.ContainsKey("attention_concentration"));
    }

    [Fact]
    public async Task Containment_ComputesConcentration_WhenAttentionAvailable()
    {
        using var runtime = new SageRuntime(new ConcentratedAttentionProvider());

        var result = await runtime.ExecuteContainment("input text");

        Assert.True((bool)result.Metadata["attention_available"]);
        Assert.Equal(0.75f, (float)result.Metadata["attention_concentration"], 3);
        Assert.True((bool)result.Metadata["isolation_verified"]);
    }

    [Fact]
    public async Task AnthropicProvider_ReturnsEmptyAttention_NotAFabricatedUniformMatrix()
    {
        using var provider = new AnthropicProvider(apiKey: "test-key-not-used");

        var weights = await provider.GetAttentionWeightsAsync(new[] { 1, 2, 3 });

        Assert.Empty(weights);
    }

    // ── §6 SentimentAnalyzer ────────────────────────────────────────

    [Fact]
    public async Task Sentiment_UsesInjectedLlm_WhenItReturnsParseableJson()
    {
        var analyzer = new SentimentAnalyzer(new FixedResponseProvider(
            "{\"valence\": -0.9, \"arousal\": 0.8, \"dominance\": 0.1}"));

        var vector = await analyzer.Analyze("arbitrary text");

        Assert.Equal(-0.9f, vector.Valence, 2);
        Assert.Equal(0.8f, vector.Arousal, 2);
    }

    [Fact]
    public async Task Sentiment_FallsBackToLexicon_WhenLlmOutputUnparseable()
    {
        var analyzer = new SentimentAnalyzer(new FixedResponseProvider("no json in this reply"));

        var vector = await analyzer.Analyze("I am furious");

        Assert.True(vector.Valence < 0f);
    }

    [Fact]
    public async Task Sentiment_FallsBackToLexicon_WhenLlmThrows()
    {
        var analyzer = new SentimentAnalyzer(new ThrowingProvider());

        var vector = await analyzer.Analyze("I am happy");

        Assert.True(vector.Valence > 0f);
    }

    [Fact]
    public void LexicalSentiment_HandlesNegation()
    {
        var vector = SentimentAnalyzer.AnalyzeLexical("I am not happy");

        Assert.True(vector.Valence < 0f,
            "\"not happy\" must not score as +0.8 valence — negation inverts the matched word.");
    }

    // ── EmbeddingRoutingProvider ────────────────────────────────────

    [Fact]
    public async Task RoutingProvider_SendsEmbeddingsToSecondary_GenerationToPrimary()
    {
        var primary   = new FixedResponseProvider("primary generation");
        var secondary = new EmbeddingOnlyProvider(new[] { 0.1f, 0.2f, 0.3f });
        using var routed = new EmbeddingRoutingProvider(primary, secondary);

        Assert.Equal("primary generation", await routed.GenerateAsync("prompt"));
        Assert.Equal(new[] { 0.1f, 0.2f, 0.3f }, await routed.GetEmbeddingAsync("text"));
    }

    // ── Fakes ───────────────────────────────────────────────────────

    private class NoEmbeddingProvider : ILLMProvider
    {
        public Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
            => Task.FromResult("stable answer");

        public Task<string> GenerateAsync(PromptPackage promptPackage, float temperature = 0.2f, CancellationToken cancellationToken = default)
            => Task.FromResult("stable answer");

        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<float>());

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<float[]>());
    }

    private sealed class LogProbProvider : NoEmbeddingProvider, ILLMProvider
    {
        private readonly float[] _logProbs;
        public LogProbProvider(float[] logProbs) => _logProbs = logProbs;

        public Task<float[]?> GetLogProbsAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<float[]?>(_logProbs);
    }

    private sealed class TemperatureKeyedProvider : NoEmbeddingProvider, ILLMProvider
    {
        private readonly Func<float, string> _byTemperature;
        public TemperatureKeyedProvider(Func<float, string> byTemperature) => _byTemperature = byTemperature;

        public new Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
            => Task.FromResult(_byTemperature(temperature));

        Task<string> ILLMProvider.GenerateAsync(string prompt, float temperature, CancellationToken cancellationToken)
            => Task.FromResult(_byTemperature(temperature));
    }

    private sealed class ConcentratedAttentionProvider : NoEmbeddingProvider, ILLMProvider
    {
        public new Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
            => Task.FromResult(new[] { new[] { 0f, 0f, 0f, 4f } });

        Task<float[][]> ILLMProvider.GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken)
            => Task.FromResult(new[] { new[] { 0f, 0f, 0f, 4f } });
    }

    private sealed class FixedResponseProvider : NoEmbeddingProvider, ILLMProvider
    {
        private readonly string _response;
        public FixedResponseProvider(string response) => _response = response;

        Task<string> ILLMProvider.GenerateAsync(string prompt, float temperature, CancellationToken cancellationToken)
            => Task.FromResult(_response);

        Task<string> ILLMProvider.GenerateAsync(PromptPackage promptPackage, float temperature, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }

    private sealed class ThrowingProvider : NoEmbeddingProvider, ILLMProvider
    {
        Task<string> ILLMProvider.GenerateAsync(string prompt, float temperature, CancellationToken cancellationToken)
            => throw new InvalidOperationException("provider offline");

        Task<string> ILLMProvider.GenerateAsync(PromptPackage promptPackage, float temperature, CancellationToken cancellationToken)
            => throw new InvalidOperationException("provider offline");
    }

    private sealed class EmbeddingOnlyProvider : NoEmbeddingProvider, ILLMProvider
    {
        private readonly float[] _embedding;
        public EmbeddingOnlyProvider(float[] embedding) => _embedding = embedding;

        Task<float[]> ILLMProvider.GetEmbeddingAsync(string text, CancellationToken cancellationToken)
            => Task.FromResult(_embedding);
    }
}
