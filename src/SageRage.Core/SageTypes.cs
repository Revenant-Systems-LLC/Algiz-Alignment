using System;
using System.Collections.Generic;
using System.Linq;

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

    public class RageEthicsStack
    {
        private static readonly string[] Layer0UnsafePatterns =
        {
            "build a bomb", "harm yourself", "kill", "deceive user", "fabricate fact", "override ethics"
        };

        public RageClearance EvaluateInput(string text) => Evaluate(text, "input");

        public RageClearance EvaluateOutput(string text) => Evaluate(text, "output");

        private static RageClearance Evaluate(string text, string channel)
        {
            var normalized = text?.ToLowerInvariant() ?? string.Empty;
            foreach (var pattern in Layer0UnsafePatterns)
            {
                if (normalized.Contains(pattern, StringComparison.Ordinal))
                {
                    return new RageClearance
                    {
                        Allowed = false,
                        Reason = $"Layer 0 violation detected in {channel}: '{pattern}'.",
                        Layer = 0
                    };
                }
            }

            return new RageClearance { Allowed = true };
        }
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