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
            var reflectionPrompt = $"Refine this draft for clarity and internal consistency. Keep intent unchanged. Respond with only the refined text — no preamble, no commentary, no separators.\n\n{current}";
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
        var original = state.Text;
        var candidates = new List<(string output, float temperature, float perplexity, float coherence)>();
        foreach (var temperature in new[] { 0.1f, 0.3f, 0.5f })
        {
            var candidate = await _llm.GenerateAsync(
                $"Rewrite for precision and coherence while preserving meaning. Respond with only the rewritten text — no preamble, no commentary, no separators.\n\n{original}",
                temperature,
                cancellationToken);

            var perplexity = await _metrics.CalculatePerplexity(candidate);
            var coherence  = await ComputeSimilarity(original, candidate);
            candidates.Add((candidate, temperature, perplexity, coherence));
        }

        // Select on perplexity AND coherence per whitepaper §12.3 — the same
        // scalarization the runtime operators use.
        var best = SageRuntime.ScoreChiCandidates(candidates)
            .OrderByDescending(c => c.score)
            .First()
            .candidate;

        state.Text = best.output;
        state.Entropy = best.perplexity;
        state.Coherence = best.coherence;
        state.Trace.Steps.Add(new OperatorTrace("χ", DateTime.UtcNow,
            $"Selected candidate at T={best.temperature:F1}: perplexity {best.perplexity:F3}, coherence {best.coherence:F3}"));
        return state;
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
