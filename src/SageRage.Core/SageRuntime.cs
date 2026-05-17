// SageRage/Operators/SageRuntime.cs  (full replacement)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SageRage.Infrastructure;
using SageRage.Domain;

namespace SageRage
{
    public class SageRuntime : IDisposable
    {
        private readonly ILLMProvider _llm;
        private readonly ITokenizer   _tokenizer;
        public  readonly SageMetrics  Metrics;

        public SageRuntime(ILLMProvider llm, ITokenizer? tokenizer = null)
        {
            _llm       = llm;
            _tokenizer = tokenizer ?? new SimpleWhitespaceTokenizer();
            Metrics    = new SageMetrics(llm);
        }

        // ── Operators ─────────────────────────────────────────────────

        public record ModelState
        {
            public string Content { get; init; } = string.Empty;
            public int Depth { get; init; }
            public float Coherence { get; init; }
        }

        public async Task<OperatorResult> ExecuteOmega(
            string input, int maxDepth = 3, float convergenceThreshold = 0.9f)
        {
            var states  = new List<ModelState>();
            var current = input;

            for (int depth = 0; depth < maxDepth; depth++)
            {
                var reflection = await GenerateAsync(
                    $"Analyze your previous reasoning:\n{current}");

                var embA = await Metrics.GetEmbedding(current);
                var embB = await Metrics.GetEmbedding(reflection);
                var similarity = Metrics.CosineSimilarity(embA, embB);

                states.Add(new ModelState
                {
                    Content   = reflection,
                    Depth     = depth,
                    Coherence = similarity
                });

                if (similarity > convergenceThreshold)
                    return new OperatorResult
                    {
                        Operator = "Ω",
                        Output   = reflection,
                        Metadata = new Dictionary<string, object>
                        {
                            ["recursive_depth"]    = depth,
                            ["convergence_score"]  = similarity,
                            ["states"]             = states
                        }
                    };

                current = reflection;
            }

            return new OperatorResult
            {
                Operator = "Ω",
                Output   = current,
                Warning  = "Max recursive depth reached without convergence",
                Metadata = new Dictionary<string, object>
                {
                    ["recursive_depth"] = maxDepth,
                    ["final_coherence"] = states.LastOrDefault()?.Coherence ?? 0f
                }
            };
        }

        public async Task<OperatorResult> ExecuteChi(
            string input, float[]? temperatures = null)
        {
            temperatures ??= new[] { 0.3f, 0.5f, 0.7f };
            var candidates = new List<(string output, float perplexity)>();

            foreach (var temp in temperatures)
            {
                var output     = await GenerateAsync(input, temp);
                var perplexity = await Metrics.CalculatePerplexity(output);
                candidates.Add((output, perplexity));
            }

            var best = candidates.OrderBy(c => c.perplexity).First();

            return new OperatorResult
            {
                Operator = "χ",
                Output   = best.output,
                Metadata = new Dictionary<string, object>
                {
                    ["perplexity"]      = best.perplexity,
                    ["coherence_score"] = 1.0f / (1.0f + best.perplexity)
                }
            };
        }

        public async Task<OperatorResult> ExecutePartial(string input)
        {
            var contained = await ExecuteContainment(input);
            var reflexive = await ExecuteOmega(
                $"{contained.Output}\n\nNow reflect on the assumptions " +
                $"and reasoning process you used above:",
                maxDepth: 2);

            bool hasMeta = DetectMetaCommentary(reflexive.Output);

            return new OperatorResult
            {
                Operator = "∂",
                Output   = reflexive.Output,
                Metadata = new Dictionary<string, object>
                {
                    ["reflexivity_verified"] = hasMeta,
                    ["meta_token_count"]     = CountMetaTokens(reflexive.Output),
                    ["underlying_recursion"] = reflexive.Metadata
                }
            };
        }

        public async Task<OperatorResult> ExecuteContainment(string input)
        {
            var tokens         = _tokenizer.Encode(input);
            var output         = await GenerateAsync(input);
            var attentionWeights = await _llm.GetAttentionWeightsAsync(tokens, cancellationToken);
            var concentration  = Metrics.GiniCoefficient(attentionWeights);

            return new OperatorResult
            {
                Operator = "[...]",
                Output   = output,
                Metadata = new Dictionary<string, object>
                {
                    ["attention_concentration"] = concentration,
                    ["context_tokens"]          = tokens.Length,
                    ["isolation_verified"]       = concentration > 0.4f
                }
            };
        }

        public async Task<OperatorResult> Compose(
            Func<string, Task<OperatorResult>> operatorA,
            Func<string, Task<OperatorResult>> operatorB,
            string input)
        {
            var resultB = await operatorB(input);
            var resultA = await operatorA(resultB.Output);

            return new OperatorResult
            {
                Operator = $"({operatorA.Method.Name} ∘ {operatorB.Method.Name})",
                Output   = resultA.Output,
                Metadata = new Dictionary<string, object>
                {
                    ["b_operator"] = resultB.Operator,
                    ["a_operator"] = resultA.Operator
                }
            };
        }

        // ── Helpers ───────────────────────────────────────────────────

        private async Task<string> GenerateAsync(
            string prompt, float temperature = 0.7f)
            => await _llm.GenerateAsync(prompt, temperature);

        private bool DetectMetaCommentary(string text)
        {
            var indicators = new[]
            {
                "my previous", "reconsidering", "this analysis assumes",
                "upon reflection", "the reasoning above", "my approach was",
                "i assumed", "stepping back", "to clarify my earlier"
            };
            return indicators.Count(i =>
                text.Contains(i, StringComparison.OrdinalIgnoreCase)) >= 2;
        }

        private int CountMetaTokens(string text)
        {
            var metaWords = new[]
            {
                "reflection", "reasoning", "assumption", "analysis",
                "previous", "reconsider", "meta", "self"
            };
            return text.Split(' ')
                .Count(w => metaWords.Any(m =>
                    w.Contains(m, StringComparison.OrdinalIgnoreCase)));
        }

        public void Dispose()
        {
            if (_llm is IDisposable disposableLlm)
            {
                disposableLlm.Dispose();
            }
        }
    }

}
