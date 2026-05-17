using System.Collections.Generic;

namespace SageRage.Guardrails
{
    public enum SageProfile { Min, Full, Strict }

    public enum TaskKind { Casual, ProceduralHelp, Factual, RecencySensitive, HighStakes }

    public sealed class QCContext
    {
        public TaskKind TaskKind { get; init; } = TaskKind.Casual;
        public bool UsedWeb { get; init; }
        public bool UsedRag { get; init; }
        public bool UsedFiles { get; init; }
        public bool HasAnchorsPi { get; init; }
        public double EntropyScore { get; init; }
        public double TensionMagnitude { get; init; }
        public bool UserRequestedSources { get; init; }
        public bool UserRequestedRecency { get; init; }
    }

    public sealed class SageCheckResult
    {
        public bool Passed { get; init; }
        public IReadOnlyList<string> Findings { get; init; } = new List<string>();
        public IReadOnlyList<string> UnsupportedClaims { get; init; } = new List<string>();
        public IReadOnlyList<string> SourceIssues { get; init; } = new List<string>();
    }
}
