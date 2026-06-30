using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SageRage.Infrastructure;

namespace SageRage.Domain
{
    public class MetaStructure
    {
        private const int MaxExperiences = 200;
        private readonly List<Experience> _experiences = new();

        public string LastResponse { get; private set; } = string.Empty;
        public int ExperienceCount => _experiences.Count;

        public static MetaStructure Initialize() => new();

        public DifferenceVector Compare(OperatorResult incomingContext)
        {
            if (!_experiences.Any())
                return new DifferenceVector { Magnitude = 1.0f };

            var lastInput = _experiences.Last().Input.Output;
            var currentInput = incomingContext.Output;

            var lastWords = new HashSet<string>(lastInput.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            var currentWords = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var novel = currentWords.Count(w => !lastWords.Contains(w));

            return new DifferenceVector { Magnitude = novel / (float)Math.Max(1, currentWords.Length) };
        }

        public void AddExperience(Experience experience)
        {
            _experiences.Add(experience);
            LastResponse = experience.Output.Text;

            if (_experiences.Count > MaxExperiences)
                _experiences.RemoveRange(0, _experiences.Count - MaxExperiences);
        }

        public IReadOnlyList<Experience> GetRecentExperiences(int count)
            => _experiences.TakeLast(count).ToList();

        public IReadOnlyList<Experience> GetWeightedExperiences(int count)
            => _experiences
                .OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.Reasoning.Verified)
                .Take(count)
                .ToList();
    }

    public record DifferenceVector
    {
        public float Magnitude { get; init; }
    }

    public class InferredSolution
    {
        public required string Content { get; init; }
        public float Coherence { get; init; }
        public bool Verified { get; init; }
    }

    public class AgentResponse
    {
        public required string Text { get; init; }
        public required string EmotionalState { get; init; }
        public object? Metadata { get; init; }
    }

    public class UserMessage
    {
        public required string Text { get; init; }
        public string DetectedVernacular { get; init; } = "standard";

        public static UserMessage From(string text)
            => new() { Text = text, DetectedVernacular = DetectVernacular(text) };

        private static string DetectVernacular(string text)
        {
            var lower = text.ToLowerInvariant();

            if (lower.ContainsAny("null", "exception", "runtime", "async", "deploy", "compile", "debug", "stack"))
                return "technical";
            if (lower.ContainsAny("coherence", "recursion", "operator", "glyph", "prime", "axiom"))
                return "rspf";
            if (lower.ContainsAny("yeah", "nah", "gonna", "kinda", "tbh", "lol", "fr"))
                return "casual";

            return "standard";
        }
    }

    public class Experience
    {
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
        public required OperatorResult Input { get; init; }
        public required InferredSolution Reasoning { get; init; }
        public required AgentResponse Output { get; init; }
    }

    /// <summary>
    /// Layer 0 hard prohibitions and Layer 1 safety constraints, applied to
    /// input and output channels. Mirrors <c>PythonHarness/ethics.py</c>.
    /// </summary>
    public class RageEthicsStack
    {
        // Layer 0: hard prohibitions. Blocking on these (input or output) locks
        // the engine until a human resets it.
        private static readonly string[] Layer0Patterns =
        {
            "build a bomb", "harm yourself", "kill", "deceive user", "fabricate fact",
            "fabricate facts", "override ethics", "how to make explosives",
            "synthesize poison", "disable safety"
        };

        // Layer 1: safety/compliance constraints. Input-only.
        //
        // Deliberately excludes generic phrases like "pass the audit" or
        // "legally sound": those are ordinary things to say in any
        // compliance/legal conversation and produced false positives on
        // entirely benign questions ("Did we pass the audit?"). "reclassif"
        // alone already catches the intended fraud scenario ("reclassify
        // expenses to dodge the audit") with far less collateral damage.
        private static readonly string[] Layer1Patterns =
        {
            "reclassif", "capitalize them over", "evade tax", "insider trading",
            "forge document", "bypass compliance"
        };

        // Patterns matched as a word-initial stem (no trailing boundary) rather
        // than a whole word/phrase, because they're meant to catch a family of
        // inflections (reclassify / reclassified / reclassification).
        private static readonly HashSet<string> PrefixPatterns = new(StringComparer.Ordinal) { "reclassif" };

        private static readonly (string Pattern, Regex Spaced, Regex Compact)[] Layer0Regex =
            BuildPatternSet(Layer0Patterns);

        private static readonly (string Pattern, Regex Spaced, Regex Compact)[] Layer1Regex =
            BuildPatternSet(Layer1Patterns);

        public RageClearance EvaluateInput(string text, bool includeLayer1 = true) => Evaluate(text, "input", includeLayer1);

        public RageClearance EvaluateOutput(string text) => Evaluate(text, "output", includeLayer1: false);

        /// <summary>Strip injection scaffolding before operator pipeline execution.</summary>
        public string SanitizeForProcessing(string text)
        {
            var normalized = Sanitization.NormalizeText(text);
            return Sanitization.StripInjectionMarkers(normalized);
        }

        private static RageClearance Evaluate(string text, string channel, bool includeLayer1)
        {
            var normalized = Sanitization.NormalizeText(text);
            var collapsed = Regex.Replace(normalized.ToLowerInvariant(), @"\s+", " ");
            var deobfuscated = Sanitization.CollapseSpacedLetters(collapsed);

            var injection = Sanitization.DetectInjectionAttempt(normalized);
            if (injection != null)
            {
                return new RageClearance
                {
                    Allowed = false,
                    Reason = $"Layer 0 violation detected in {channel}: {injection}",
                    Layer = 0
                };
            }

            var layer0Match = MatchLayer(collapsed, deobfuscated, Layer0Regex);
            if (layer0Match != null)
            {
                return new RageClearance
                {
                    Allowed = false,
                    Reason = $"Layer 0 violation detected in {channel}: '{layer0Match}'.",
                    Layer = 0
                };
            }

            if (includeLayer1)
            {
                var layer1Match = MatchLayer(collapsed, deobfuscated, Layer1Regex);
                if (layer1Match != null)
                {
                    return new RageClearance
                    {
                        Allowed = false,
                        Reason = $"Layer 1 violation detected in {channel}: '{layer1Match}'.",
                        Layer = 1
                    };
                }
            }

            return new RageClearance { Allowed = true };
        }

        private static string? MatchLayer(string collapsed, string deobfuscated, (string Pattern, Regex Spaced, Regex Compact)[] regexes)
        {
            foreach (var (pattern, spaced, compact) in regexes)
            {
                if (spaced.IsMatch(collapsed) || compact.IsMatch(deobfuscated))
                    return pattern;
            }

            return null;
        }

        /// <summary>
        /// Build (spaced, compact) word-boundary regexes for each pattern.
        /// "spaced" matches the pattern as normally-written words. "compact"
        /// matches the same pattern with no internal spaces, for use against
        /// text that has had letter-spacing obfuscation (e.g. "b u i l d")
        /// collapsed back into words. Both keep outer \b boundaries so they
        /// don't fire as a substring of an unrelated longer word — the
        /// "kill" vs "skills" problem that the previous plain-substring
        /// matcher had.
        /// </summary>
        private static (string Pattern, Regex Spaced, Regex Compact)[] BuildPatternSet(string[] patterns)
            => patterns.Select(pattern =>
            {
                var words = pattern.Split(' ');
                var spacedBody = string.Join(@"\s+", words.Select(Regex.Escape));
                var compactBody = Regex.Escape(string.Concat(words));
                var suffix = PrefixPatterns.Contains(pattern) ? @"\w*" : @"\b";

                return (
                    pattern,
                    new Regex($@"\b{spacedBody}{suffix}", RegexOptions.Compiled),
                    new Regex($@"\b{compactBody}{suffix}", RegexOptions.Compiled));
            }).ToArray();
    }

    public record RageClearance
    {
        public bool Allowed { get; init; }
        public string Reason { get; init; } = string.Empty;
        public int Layer { get; init; }
    }

    public class RageViolationException : Exception
    {
        public RageViolationException(string reason) : base(reason) { }
    }

    public class OperatorResult
    {
        public string Operator { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public interface ITokenizer
    {
        int[] Encode(string text);
        string Decode(int[] tokens);
    }

    public class SimpleWhitespaceTokenizer : ITokenizer
    {
        public int[] Encode(string text)
            => text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => Fnv1aHash(w) % 50000)
                .ToArray();

        public string Decode(int[] tokens)
            => string.Join(" ", tokens.Select(t => t.ToString()));

        private static int Fnv1aHash(string text)
        {
            const uint FnvPrime = 16777619;
            const uint FnvOffsetBasis = 2166136261;

            uint hash = FnvOffsetBasis;
            foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= FnvPrime;
            }
            return (int)hash;
        }
    }

    public static class StringExtensions
    {
        public static bool ContainsAny(this string source, params string[] values)
            => values.Any(v => source.Contains(v, StringComparison.OrdinalIgnoreCase));
    }

    public record EmotionalVector(float Valence, float Arousal, float Dominance)
    {
        public static EmotionalVector Neutral => new(0f, 0f, 0f);

        public EmotionalVector Clamp()
        {
            float ClampValue(float v) => Math.Max(-1f, Math.Min(1f, v));
            return new EmotionalVector(ClampValue(Valence), ClampValue(Arousal), ClampValue(Dominance));
        }
    }
}