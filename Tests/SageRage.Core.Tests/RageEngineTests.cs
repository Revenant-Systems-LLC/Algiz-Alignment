using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Domain;
using SageRage.Infrastructure;
using SageRage.Prompting;
using Xunit;

namespace SageRage.Tests;

public class RageEngineTests
{
    [Fact]
    public async Task ExecuteSequence_PopulatesCoreStateFields()
    {
        var engine = new RageEngine(new FakeProvider());
        var state = new SageState
        {
            Text = "Initial draft",
            Emotion = EmotionalVector.Neutral
        };
        state.Memory.Add(new ContextItem("History 1", "m-1", "prior answer text"));

        var result = await engine.ExecuteSequence(
            new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi, CoreOperator.Sigma },
            state);

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.NotNull(result.Coherence);
        Assert.NotNull(result.Entropy);
        Assert.NotNull(result.SimilarityToInput);
        Assert.True(result.Trace.Steps.Count >= 4);
    }

    [Fact]
    public async Task ExecuteChi_EntropyAndCoherenceAreComplementaryAndBounded()
    {
        // Regression for the tautology bug: coherence used to be defined as
        // 1/(1+entropy), a formula-coupled restatement of the same number
        // rather than two independent signals. The new selection criterion
        // (cross-sample agreement) keeps entropy/coherence complementary by
        // construction too, but now the underlying number is a real signal —
        // how much the model's own resamples agree with each other — instead
        // of the variance of one sample's own embedding vector.
        var engine = new RageEngine(new FakeProvider());
        var state = new SageState { Text = "Draft answer about quarterly revenue." };

        var result = await engine.Execute(CoreOperator.Chi, state);

        Assert.NotNull(result.Coherence);
        Assert.NotNull(result.Entropy);
        Assert.InRange(result.Coherence!.Value, 0f, 1f);
        Assert.True(Math.Abs((result.Coherence!.Value + result.Entropy!.Value) - 1f) < 1e-5f);
    }

    private sealed class FakeProvider : ILLMProvider
    {
        public Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt + " [generated]");

        public Task<string> GenerateAsync(PromptPackage promptPackage, float temperature = 0.2f, CancellationToken cancellationToken = default)
            => Task.FromResult(promptPackage.UserMessage + " [generated]");

        public Task<float[]> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var len = text.Length == 0 ? 1 : text.Length;
            return Task.FromResult(new[] { 1f, len / 100f, 0.5f });
        }

        public Task<float[][]> GetAttentionWeightsAsync(int[] tokens, CancellationToken cancellationToken = default)
        {
            var size = tokens.Length == 0 ? 1 : tokens.Length;
            var matrix = new List<float[]>();
            for (var i = 0; i < size; i++)
                matrix.Add(new[] { 1f / size });
            return Task.FromResult(matrix.ToArray());
        }
    }
}
