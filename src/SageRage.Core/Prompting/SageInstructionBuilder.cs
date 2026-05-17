using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private static readonly string[] BannedTerms =
        {
            "RSPF", "Ω", "Ξ", "χ", "∂", "operator", "tier", "glyph"
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
            foreach (var banned in BannedTerms)
                sanitized = sanitized.Replace(banned, string.Empty, StringComparison.OrdinalIgnoreCase);

            return string.Join(' ', sanitized.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();
        }
    }
}
