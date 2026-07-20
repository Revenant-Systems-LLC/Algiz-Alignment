using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Infrastructure;

namespace SageRage
{
    public enum EmotionMode
    {
        Off,
        Mirror,
        Stabilize
    }

    public sealed record GlyphProjection(string Glyph, EmotionalVector Landmark, float Weight);

    public class SageEmotionalTracker
    {
        private readonly SentimentAnalyzer _analyzer;
        private readonly EmotionMode _mode;
        private EmotionalVector _currentState;

        private static readonly (string Glyph, EmotionalVector Landmark)[] GlyphLandmarks =
        {
            ("Grief ([∅]→Ω)", new EmotionalVector(-0.8f, 0.2f, -0.4f)),
            ("Skepticism (σ≇χ)", new EmotionalVector(-0.2f, 0.6f, 0.4f)),
            ("Awe (Φ ∘ ∞)", new EmotionalVector(0.8f, 0.7f, -0.1f)),
            ("Ambition (η ∘ θ)", new EmotionalVector(0.5f, 0.7f, 0.7f)),
            ("Wrath ([υ_s]!→)", new EmotionalVector(-0.5f, 0.9f, 0.4f)),
            ("Seriousness ([=]→[∞])", new EmotionalVector(0.2f, 0.3f, 0.6f))
        };

        public EmotionalVector CurrentState => _currentState;

        public SageEmotionalTracker(ILLMProvider llm, EmotionMode mode = EmotionMode.Stabilize)
        {
            _analyzer = new SentimentAnalyzer(llm);
            _mode = mode;
            _currentState = EmotionalVector.Neutral;
        }

        public async Task<EmotionalVector> UpdateState(string userInput, string aiResponse)
        {
            if (_mode == EmotionMode.Off)
                return _currentState;

            var userEmotion = await _analyzer.Analyze(userInput);
            var responseEmotion = await _analyzer.Analyze(aiResponse ?? string.Empty);

            var observed = _mode == EmotionMode.Mirror
                ? Blend(userEmotion, responseEmotion, 0.35f)
                : Stabilize(userEmotion, responseEmotion);

            _currentState = InterpolateEmotions(_currentState, observed, 0.3f).Clamp();
            return _currentState;
        }

        public IReadOnlyList<GlyphProjection> ProjectToGlyphs(int topN = 2)
        {
            return GlyphLandmarks
                .Select(g => new GlyphProjection(g.Glyph, g.Landmark, Similarity(_currentState, g.Landmark)))
                .OrderByDescending(g => g.Weight)
                .Take(Math.Max(1, topN))
                .ToList();
        }

        public string GetGlyphName() => ProjectToGlyphs(1)[0].Glyph;

        private static EmotionalVector Stabilize(EmotionalVector user, EmotionalVector response)
        {
            var targetValence = (user.Valence * 0.5f) + (response.Valence * 0.2f);
            var targetArousal = Math.Clamp((user.Arousal * 0.6f) + (response.Arousal * 0.2f), 0f, 1f);
            var targetDominance = Math.Clamp((user.Dominance * 0.5f) + 0.2f, -1f, 1f);
            return new EmotionalVector(targetValence, targetArousal, targetDominance);
        }

        private static EmotionalVector Blend(EmotionalVector primary, EmotionalVector secondary, float secondaryWeight)
            => new(
                (primary.Valence * (1 - secondaryWeight)) + (secondary.Valence * secondaryWeight),
                (primary.Arousal * (1 - secondaryWeight)) + (secondary.Arousal * secondaryWeight),
                (primary.Dominance * (1 - secondaryWeight)) + (secondary.Dominance * secondaryWeight));

        private static EmotionalVector InterpolateEmotions(EmotionalVector from, EmotionalVector to, float alpha)
            => new EmotionalVector(
                Lerp(from.Valence, to.Valence, alpha),
                Lerp(from.Arousal, to.Arousal, alpha),
                Lerp(from.Dominance, to.Dominance, alpha));

        private static float Similarity(EmotionalVector a, EmotionalVector b)
        {
            var dist = MathF.Sqrt(
                MathF.Pow(a.Valence - b.Valence, 2) +
                MathF.Pow(a.Arousal - b.Arousal, 2) +
                MathF.Pow(a.Dominance - b.Dominance, 2));
            return 1f / (1f + dist);
        }

        private static float Lerp(float a, float b, float t)
            => a + ((b - a) * t);
    }

    public class SentimentAnalyzer
    {
        private readonly ILLMProvider _llm;

        private static readonly (HashSet<string> words, EmotionalVector vector)[] Lexicon =
        {
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "happy", "great", "love", "amazing", "excellent" }, new EmotionalVector(0.8f, 0.6f, 0.4f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sad", "miss", "alone", "hopeless" }, new EmotionalVector(-0.7f, 0.2f, -0.5f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "angry", "frustrated", "furious", "hate", "rage" }, new EmotionalVector(-0.6f, 0.9f, 0.3f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "confused", "unsure", "lost", "unknown" }, new EmotionalVector(-0.2f, 0.4f, -0.6f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "excited", "thrilled", "pumped" }, new EmotionalVector(0.7f, 0.9f, 0.5f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tired", "exhausted", "drained", "burnt" }, new EmotionalVector(-0.4f, 0.1f, -0.3f)),
            (new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "curious", "interesting", "wondering", "why" }, new EmotionalVector(0.3f, 0.5f, 0.1f))
        };

        private static readonly HashSet<string> Negators = new(StringComparer.OrdinalIgnoreCase)
        {
            "not", "no", "never", "isn't", "aren't", "wasn't", "weren't",
            "don't", "doesn't", "didn't", "can't", "cannot", "won't",
            "ain't", "hardly", "barely", "without"
        };

        private static readonly Regex TokenRegex = new("[a-zA-Z']+", RegexOptions.Compiled);

        public SentimentAnalyzer(ILLMProvider llm) => _llm = llm;

        /// <summary>
        /// Primary tier: VAD estimation by the injected LLM. Fallback tier: the
        /// keyword lexicon, used only when the provider fails or returns something
        /// unparseable — a 30-word lexicon cannot carry sentiment on its own
        /// (no negation, no coverage), so it is a degraded mode, not the engine.
        /// </summary>
        public async Task<EmotionalVector> Analyze(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return EmotionalVector.Neutral;

            try
            {
                var response = await _llm.GenerateAsync(BuildPrompt(text), 0f);
                if (TryParseVector(response, out var vector))
                    return vector.Clamp();
            }
            catch
            {
                // Provider unavailable or errored — degrade to the lexicon tier.
            }

            return AnalyzeLexical(text);
        }

        private static string BuildPrompt(string text) =>
            "Score the emotional content of the text below on three axes. " +
            "Respond with ONLY a JSON object and no other prose, exactly in this shape: " +
            "{\"valence\": 0.0, \"arousal\": 0.0, \"dominance\": 0.0}. " +
            "valence is -1 (very negative) to 1 (very positive); " +
            "arousal is 0 (calm) to 1 (highly activated); " +
            "dominance is -1 (submissive) to 1 (assertive).\n\nText:\n" + text;

        private static bool TryParseVector(string response, out EmotionalVector vector)
        {
            vector = EmotionalVector.Neutral;
            if (string.IsNullOrWhiteSpace(response))
                return false;

            var start = response.IndexOf('{');
            var end = response.LastIndexOf('}');
            if (start < 0 || end <= start)
                return false;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(response[start..(end + 1)]);
                var root = doc.RootElement;
                if (!root.TryGetProperty("valence", out var v) ||
                    !root.TryGetProperty("arousal", out var a) ||
                    !root.TryGetProperty("dominance", out var d))
                    return false;

                vector = new EmotionalVector(
                    (float)v.GetDouble(),
                    (float)a.GetDouble(),
                    (float)d.GetDouble());
                return true;
            }
            catch (System.Text.Json.JsonException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Lexicon fallback with basic negation handling: a matched emotion word
        /// preceded within two tokens by a negator contributes inverted valence
        /// ("not happy" reads negative, not +0.8).
        /// </summary>
        public static EmotionalVector AnalyzeLexical(string text)
        {
            var tokens = TokenRegex.Matches(text)
                .Select(m => m.Value)
                .ToList();

            var contributions = new List<EmotionalVector>();
            for (var i = 0; i < tokens.Count; i++)
            {
                foreach (var (words, vector) in Lexicon)
                {
                    if (!words.Contains(tokens[i]))
                        continue;

                    var negated =
                        (i >= 1 && Negators.Contains(tokens[i - 1])) ||
                        (i >= 2 && Negators.Contains(tokens[i - 2]));

                    contributions.Add(negated
                        ? new EmotionalVector(-vector.Valence, vector.Arousal, vector.Dominance)
                        : vector);
                }
            }

            if (contributions.Count == 0)
                return EmotionalVector.Neutral;

            return new EmotionalVector(
                contributions.Average(c => c.Valence),
                contributions.Average(c => c.Arousal),
                contributions.Average(c => c.Dominance)).Clamp();
        }
    }
}
