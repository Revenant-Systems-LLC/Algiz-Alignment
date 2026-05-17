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

        private static readonly Regex TokenRegex = new("[a-zA-Z']+", RegexOptions.Compiled);

        public SentimentAnalyzer(ILLMProvider llm) => _llm = llm;

        public Task<EmotionalVector> Analyze(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Task.FromResult(EmotionalVector.Neutral);

            var tokens = TokenRegex.Matches(text)
                .Select(m => m.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var matches = Lexicon.Where(x => x.words.Overlaps(tokens)).ToArray();
            if (matches.Length == 0)
                return Task.FromResult(EmotionalVector.Neutral);

            var valence = matches.Average(m => m.vector.Valence);
            var arousal = matches.Average(m => m.vector.Arousal);
            var dominance = matches.Average(m => m.vector.Dominance);

            return Task.FromResult(new EmotionalVector((float)valence, (float)arousal, (float)dominance).Clamp());
        }
    }
}
