using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Infrastructure;
using SageRage.Prompting;

namespace SageRage;

public enum CoreOperator
{
    Containment,
    Omega,
    Chi,
    Sigma
}

public sealed class EthicalFlags
{
    public bool Layer0Clear { get; set; } = true;
    public bool Layer1Clear { get; set; } = true;
    public bool Layer2Clear { get; set; } = true;
    public bool Layer3Clear { get; set; } = true;
    public List<string> Violations { get; } = new();
}

public sealed class MetaTrace
{
    public List<OperatorTrace> Steps { get; } = new();
}

public sealed record OperatorTrace(string Operator, DateTime Timestamp, string Note);

public sealed class SageState
{
    public string Text { get; set; } = string.Empty;
    public EmotionalVector Emotion { get; set; } = EmotionalVector.Neutral;
    public float? Coherence { get; set; }
    public float? Entropy { get; set; }
    public float? SimilarityToInput { get; set; }
    public List<ContextItem> Memory { get; } = new();
    public EthicalFlags Ethics { get; } = new();
    public MetaTrace Trace { get; } = new();
}

public sealed class RageEngine
{
    private readonly ILLMProvider _llm;
    private readonly SageMetrics _metrics;

    public RageEngine(ILLMProvider llm)
    {
        _llm = llm;
        _metrics = new SageMetrics(llm);
    }

    public Task<SageState> Execute(CoreOperator op, SageState state, CancellationToken cancellationToken = default)
        => op switch
        {
            CoreOperator.Containment => ExecuteContainment(state),
            CoreOperator.Omega => ExecuteOmega(state, cancellationToken),
            CoreOperator.Chi => ExecuteChi(state, cancellationToken),
            CoreOperator.Sigma => ExecuteSigma(state, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unsupported core operator")
        };

    public async Task<SageState> ExecuteSequence(IEnumerable<CoreOperator> ops, SageState state, CancellationToken cancellationToken = default)
    {
        foreach (var op in ops)
            state = await Execute(op, state, cancellationToken);

        return state;
    }

    private Task<SageState> ExecuteContainment(SageState state)
    {
        state.Text = state.Text.Trim();

        const int maxMemoryItems = 6;
        const int maxSnippetChars = 800;

        if (state.Memory.Count > maxMemoryItems)
            state.Memory.RemoveRange(0, state.Memory.Count - maxMemoryItems);

        for (var i = 0; i < state.Memory.Count; i++)
        {
            var item = state.Memory[i];
            if (item.Snippet.Length > maxSnippetChars)
                state.Memory[i] = new ContextItem(item.Title, item.SourceId, item.Snippet[..maxSnippetChars], item.Url, item.Timestamp);
        }

        state.Trace.Steps.Add(new OperatorTrace("[...]", DateTime.UtcNow, $"Memory bounded to {state.Memory.Count} items"));
        return Task.FromResult(state);
    }

    private async Task<SageState> ExecuteOmega(SageState state, CancellationToken cancellationToken)
    {
        const int maxDepth = 3;
        const float convergenceThreshold = 0.92f;

        var baseline = state.Text;
        var current = baseline;

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            var reflectionPrompt = $"Refine this draft for clarity and internal consistency. Keep intent unchanged.\n\n{current}";
            var candidate = await _llm.GenerateAsync(reflectionPrompt, 0.2f, cancellationToken);
            var similarity = await ComputeSimilarity(current, candidate);
            current = candidate;

            if (similarity >= convergenceThreshold)
            {
                state.Text = current;
                state.SimilarityToInput = await ComputeSimilarity(baseline, current);
                state.Trace.Steps.Add(new OperatorTrace("Ω", DateTime.UtcNow, $"Converged at depth {depth}"));
                return state;
            }
        }

        state.Text = current;
        state.SimilarityToInput = await ComputeSimilarity(baseline, current);
        state.Trace.Steps.Add(new OperatorTrace("Ω", DateTime.UtcNow, "Stopped at max depth without convergence"));
        return state;
    }

    private async Task<SageState> ExecuteChi(SageState state, CancellationToken cancellationToken)
    {
        // Sample independently at several temperatures and select the candidate
        // the other samples most agree with (self-consistency). A model confident
        // in its answer tends to reproduce similar content across resamples,
        // while a hallucinated or unstable answer tends to be an outlier relative
        // to its own resamples — a real signal. The previous implementation
        // measured the internal variance of a single candidate's own embedding
        // vector and called it "entropy", and defined "coherence" as 1/(1+entropy)
        // — a number with no relationship to model confidence, and mathematically
        // guaranteed to agree with "entropy" by construction, making the
        // documented two-factor selection ("lowest entropy AND highest
        // coherence") tautological rather than a real check.
        var samples = new List<string>();
        foreach (var temperature in new[] { 0.1f, 0.3f, 0.5f })
        {
            var candidate = await _llm.GenerateAsync(
                $"Rewrite for precision and coherence while preserving meaning:\n\n{state.Text}",
                temperature,
                cancellationToken);
            samples.Add(candidate);
        }

        var (bestIndex, bestAgreement) = await SelectConsensus(samples, cancellationToken);
        state.Text = samples[bestIndex];
        state.Coherence = bestAgreement;
        state.Entropy = 1f - bestAgreement;
        state.Trace.Steps.Add(new OperatorTrace("χ", DateTime.UtcNow,
            $"Selected consensus candidate (agreement={bestAgreement:F3}) across {samples.Count} samples"));
        return state;
    }

    /// <summary>
    /// Pick the sample the others most agree with (mean pairwise similarity).
    /// Returns (index, agreement) for the winning sample.
    /// </summary>
    private async Task<(int index, float agreement)> SelectConsensus(IReadOnlyList<string> samples, CancellationToken cancellationToken)
    {
        if (samples.Count <= 1)
            return (0, 1f);

        var agreement = new float[samples.Count];
        for (var i = 0; i < samples.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            float sum = 0f;
            for (var j = 0; j < samples.Count; j++)
            {
                if (i == j) continue;
                sum += await ComputeSimilarity(samples[i], samples[j]);
            }
            agreement[i] = sum / (samples.Count - 1);
        }

        var bestIndex = 0;
        for (var i = 1; i < agreement.Length; i++)
            if (agreement[i] > agreement[bestIndex])
                bestIndex = i;

        return (bestIndex, agreement[bestIndex]);
    }

    private async Task<SageState> ExecuteSigma(SageState state, CancellationToken cancellationToken)
    {
        var memoryContext = state.Memory.Count == 0
            ? "No prior memory context provided."
            : string.Join("\n", state.Memory.Select(m => $"- {m.Snippet}"));

        var critiquePrompt =
            "Act as a skeptical verifier. Identify unsupported claims or contradictions versus context. " +
            "If issues exist, revise to remove unsupported content and return only the revised answer.\n\n" +
            $"Context:\n{memoryContext}\n\nDraft:\n{state.Text}";

        var revised = await _llm.GenerateAsync(critiquePrompt, 0.1f, cancellationToken);
        state.Text = revised.Trim();
        state.Trace.Steps.Add(new OperatorTrace("σ", DateTime.UtcNow, "Applied skeptical contrast against memory context"));
        return state;
    }

    private async Task<float> ComputeSimilarity(string left, string right)
    {
        var leftEmbedding = await _metrics.GetEmbedding(left);
        var rightEmbedding = await _metrics.GetEmbedding(right);

        if (leftEmbedding.Length > 0 && leftEmbedding.Length == rightEmbedding.Length)
            return _metrics.CosineSimilarity(leftEmbedding, rightEmbedding);

        return ComputeLexicalSimilarity(left, right);
    }

    private static float ComputeLexicalSimilarity(string left, string right)
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
}
