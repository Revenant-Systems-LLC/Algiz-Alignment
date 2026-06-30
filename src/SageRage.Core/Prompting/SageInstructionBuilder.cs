using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SageRage.Prompting
{
    public enum InstructionTemplate
    {
        ConciseHelpfulAssistant,
        StrictAccuracyWithCitations,
        RefusalAskClarifyingQuestions
    }

    public sealed class ExecutiveInstructionOptions
    {
        public InstructionTemplate Template { get; init; } = InstructionTemplate.ConciseHelpfulAssistant;
        public string? ToneIntent { get; init; }
        public bool RefusalMode { get; init; }
        public IReadOnlyList<string> RequiredConstraints { get; init; } = Array.Empty<string>();
        public int MaxLength { get; init; } = 600;
    }

    public sealed class SageInstructionBuilder
    {
        // Plain-word terms: stripped only on a whole-word match. A naive substring
        // Replace here silently mangles unrelated words that happen to contain one
        // of these as a substring — "frontier" loses its last two letters because
        // it contains "tier"; "operators" and "cooperator" have the same problem.
        private static readonly string[] BannedWordTerms =
        {
            "RSPF", "operator", "tier", "glyph"
        };

        // Unicode glyph symbols: safe to strip via plain substring replace since
        // they essentially never occur as a fragment of an unrelated word.
        private static readonly string[] BannedSymbolTerms =
        {
            "Ω", "Ξ", "χ", "∂"
        };

        public string Build(ExecutiveInstructionOptions options)
        {
            var baseInstruction = options.Template switch
            {
                InstructionTemplate.StrictAccuracyWithCitations =>
                    "You are a precise assistant. Prioritize factual correctness, clearly mark uncertainty, and cite provided sources when available.",
                InstructionTemplate.RefusalAskClarifyingQuestions =>
                    "If required information is missing or the request is unsafe, politely decline and ask concise clarifying questions.",
                _ =>
                    "You are a concise, helpful assistant. Give direct answers and keep the response practical."
            };

            var sb = new StringBuilder(baseInstruction);

            if (!string.IsNullOrWhiteSpace(options.ToneIntent))
                sb.Append($" Tone: {options.ToneIntent.Trim()}.");

            if (options.RequiredConstraints.Any())
                sb.Append(" Constraints: " + string.Join("; ", options.RequiredConstraints.Where(c => !string.IsNullOrWhiteSpace(c))) + ".");

            if (options.RefusalMode)
                sb.Append(" If confidence is low, ask for missing details before answering.");

            var sanitized = Sanitize(sb.ToString());
            var max = Math.Max(1, options.MaxLength);
            return sanitized.Length > max ? sanitized[..max] : sanitized;
        }

        private static string Sanitize(string text)
        {
            var sanitized = text;
            foreach (var banned in BannedWordTerms)
                sanitized = Regex.Replace(sanitized, $@"\b{Regex.Escape(banned)}\b", string.Empty, RegexOptions.IgnoreCase);
            foreach (var banned in BannedSymbolTerms)
                sanitized = sanitized.Replace(banned, string.Empty, StringComparison.OrdinalIgnoreCase);

            return string.Join(' ', sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        }
    }
}
