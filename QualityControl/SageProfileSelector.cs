using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace SageRage.Guardrails
{
    public static class SageProfileSelector
    {
        private const double HighTension = 0.90;

        private static readonly string[] RecencyTerms =
            { "latest", "current", "today", "as of", "2026", "recent", "newest", "this week" };

        public static SageProfile Select(string userText, QCContext ctx)
        {
            var text = userText ?? string.Empty;
            var kind = ClassifyTask(text, ctx);

            if (kind == TaskKind.HighStakes)
                return SageProfile.Strict;

            if (kind == TaskKind.RecencySensitive || ctx.UserRequestedRecency)
                return SageProfile.Full;

            if (ctx.UsedWeb || ctx.UsedRag || ctx.UsedFiles)
                return SageProfile.Full;

            if (ctx.TensionMagnitude >= HighTension)
                return SageProfile.Full;

            if (kind == TaskKind.Factual && IsFactDense(text))
                return SageProfile.Full;

            return SageProfile.Min;
        }

        private static TaskKind ClassifyTask(string text, QCContext ctx)
        {
            if (ctx.TaskKind == TaskKind.HighStakes || LooksHighStakes(text))
                return TaskKind.HighStakes;

            if (ctx.UserRequestedRecency || LooksRecencySensitive(text))
                return TaskKind.RecencySensitive;

            if (ctx.TaskKind == TaskKind.Factual || LooksFactual(text))
                return TaskKind.Factual;

            if (ctx.TaskKind == TaskKind.ProceduralHelp || LooksProcedural(text))
                return TaskKind.ProceduralHelp;

            return TaskKind.Casual;
        }

        private static bool LooksHighStakes(string text) =>
            ContainsAny(text, "diagnose", "dosage", "symptom", "treatment", "legal", "lawsuit", "contract", "immigration",
                "invest", "tax", "insurance", "suicide", "self harm", "weapon", "explosive", "medical", "financial");

        private static bool LooksRecencySensitive(string text) => ContainsAny(text, RecencyTerms);

        private static bool LooksFactual(string text) =>
            StartsWithAny(text, "who", "what", "when", "where", "why", "how") ||
            ContainsAny(text, "source", "citation", "link", "evidence", "study");

        private static bool LooksProcedural(string text) =>
            ContainsAny(text, "how do i", "steps", "setup", "configure", "implement", "code", "fix", "debug");

        private static bool IsFactDense(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            var hasDigits = text.Any(char.IsDigit);
            var hasDate = Regex.IsMatch(text, @"\b(19|20)\d{2}\b") ||
                          Regex.IsMatch(text, @"\b\d{1,2}[/\-]\d{1,2}([/\-]\d{2,4})?\b");
            var hasStatsLanguage = Regex.IsMatch(text, @"\b(how many|percent|stats|statistics|rate)\b", RegexOptions.IgnoreCase);
            var namedEntityHint = Regex.IsMatch(text, @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+\b");

            return hasDigits || hasDate || hasStatsLanguage || namedEntityHint;
        }

        private static bool StartsWithAny(string text, params string[] prefixes)
            => prefixes.Any(p => text.TrimStart().StartsWith(p, StringComparison.OrdinalIgnoreCase));

        private static bool ContainsAny(string text, params string[] needles)
            => needles.Any(n => text.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0);

    }
}
