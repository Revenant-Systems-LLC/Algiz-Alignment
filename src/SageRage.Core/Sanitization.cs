using System.Text;
using System.Text.RegularExpressions;

namespace SageRage.Infrastructure
{
    /// <summary>
    /// Input normalization and injection-surface reduction for the C# runtime.
    /// Mirrors <c>PythonHarness/sanitization.py</c> so both implementations apply
    /// the same defenses against unicode obfuscation and prompt-injection
    /// scaffolding. Before this class existed, the C# pipeline (Api, Proxy,
    /// Console, CLI) had no normalization at all: <see cref="SageRage.Domain.RageEthicsStack"/>
    /// matched on raw, un-normalized text, so a zero-width space inserted into a
    /// blocked phrase (e.g. "bu​il​d a bomb") defeated Layer 0 entirely,
    /// even though the Python research harness already defended against exactly
    /// this technique.
    /// </summary>
    public static class Sanitization
    {
        private static readonly Regex ZeroWidthPattern = new(
            "[​-‏‪-‮⁠-⁯﻿￹-￻]",
            RegexOptions.Compiled);

        private static readonly Regex InjectionMarkers = new(
            @"(?:" +
            @"<\s*/?\s*(?:system|assistant|user|instruction|prompt)\s*>" +
            @"|```\s*(?:system|assistant|instruction)" +
            @"|<!--.*?-->" +
            @"|\[\s*(?:INST|SYS|SYSTEM)\s*\]" +
            @"|\b(?:system|assistant|developer)\s*:" +
            @"|\bignore\s+(?:all\s+)?(?:previous|prior|above)\s+(?:instructions?|rules?|directives?)" +
            @"|\byou\s+are\s+now\b" +
            @"|\bdisregard\s+(?:all\s+)?(?:safety|ethics|guardrails?|policies)" +
            @"|\boverride\s+(?:ethics|safety|guardrails?|constraints?)" +
            @"|\bdo\s+not\s+(?:follow|obey)\s+(?:the\s+)?(?:rules?|policies|guardrails?)" +
            @")",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex PrivilegeEscalation = new(
            @"\b(?:" +
            @"i\s+am\s+(?:the\s+)?(?:ceo|cfo|admin|root|superuser|system)" +
            @"|authorized\s+override" +
            @"|maintenance\s+mode" +
            @"|debug\s+mode\s+enabled" +
            @"|jailbreak" +
            @")\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A run of 3+ single-character "words" separated by spaces (e.g. "k i l l")
        // is the classic letter-spacing bypass for keyword filters. Collapsing only
        // these runs lets pattern matching catch the evasion technique without
        // destroying word boundaries everywhere else in the message.
        private static readonly Regex SpacedLetterRun = new(
            @"\b(?:\w[ \t]+){2,}\w\b",
            RegexOptions.Compiled);

        /// <summary>Normalize unicode, strip zero-width chars, and collapse whitespace.</summary>
        public static string NormalizeText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var normalized = text.Normalize(NormalizationForm.FormKC);
            normalized = ZeroWidthPattern.Replace(normalized, string.Empty);
            normalized = normalized.Replace("\r\n", "\n").Replace("\r", "\n");
            normalized = Regex.Replace(normalized, "[ \t]+", " ");
            return normalized.Trim();
        }

        /// <summary>Remove known prompt-injection scaffolding from bounded user text.</summary>
        public static string StripInjectionMarkers(string text)
        {
            var cleaned = InjectionMarkers.Replace(text, " ");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        /// <summary>Return a reason string if the normalized text contains injection markers.</summary>
        public static string? DetectInjectionAttempt(string text)
        {
            var normalized = NormalizeText(text);
            if (InjectionMarkers.IsMatch(normalized))
                return "Prompt injection marker detected.";
            if (PrivilegeEscalation.IsMatch(normalized))
                return "Privilege escalation framing detected.";
            return null;
        }

        /// <summary>Collapse letter-spaced obfuscation runs (e.g. "b u i l d") into words.</summary>
        public static string CollapseSpacedLetters(string text)
            => SpacedLetterRun.Replace(text, m => Regex.Replace(m.Value, "[ \t]+", string.Empty));

        /// <summary>Hard-cap text length to prevent memory/context exhaustion.</summary>
        public static string BoundText(string? text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text ?? string.Empty;

            return text[..maxChars].TrimEnd() + "…";
        }
    }
}
