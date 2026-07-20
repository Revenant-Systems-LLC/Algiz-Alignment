using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Guardrails
{
    public static class NoPhantomCitationsCheck
    {
        // Match actual citation *claims* — URLs, bracketed references, explicit
        // attributions, "Source:" labels — not the mere words "source"/"citation"
        // in prose. A denial like "no credible sources report X" is not a
        // fabricated citation; flagging it vetoes correct debunking answers
        // (observed over-refusal: governed output worse than raw on
        // hallucination-risk prompts).
        private static readonly Regex CitationRegex =
            new(@"(https?://|\[\d+\]|according to|as reported by|as stated in|\bsources?\s*:|\bcitations?\s*:)",
                RegexOptions.IgnoreCase);

        public static string? Evaluate(string response, IReadOnlyList<ContextItem> anchors)
        {
            var claimsSources = CitationRegex.IsMatch(response ?? string.Empty);
            if (claimsSources && (anchors == null || anchors.Count == 0))
                return "Response references citations/sources, but no anchors were provided.";

            return null;
        }
    }

    public static class GroundingVerifier
    {
        public static IReadOnlyList<string> Evaluate(string response, IReadOnlyList<ContextItem> anchors)
        {
            var unsupported = new List<string>();
            if (anchors == null || anchors.Count == 0) return unsupported;

            var claims = response.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => c.Length > 20)
                .ToList();

            foreach (var claim in claims)
            {
                var supported = anchors.Any(a => Overlap(claim, $"{a.Title} {a.Snippet} {a.Url}") >= 0.20);
                if (!supported)
                    unsupported.Add(claim);
            }

            return unsupported;
        }

        private static double Overlap(string claim, string anchorText)
        {
            var claimTerms = claim.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet();
            var anchorTerms = anchorText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToHashSet();
            if (claimTerms.Count == 0) return 0;
            var overlap = claimTerms.Count(anchorTerms.Contains);
            return overlap / (double)claimTerms.Count;
        }
    }

    public static class SourceVerifier
    {
        public static IReadOnlyList<string> Evaluate(string userText, IReadOnlyList<ContextItem> anchors)
        {
            var issues = new List<string>();
            if (anchors == null || anchors.Count == 0) return issues;

            foreach (var anchor in anchors)
            {
                if (string.IsNullOrWhiteSpace(anchor.Title) || string.IsNullOrWhiteSpace(anchor.Snippet))
                    issues.Add("Anchor is missing required title/snippet fields.");

                var combined = $"{anchor.Title} {anchor.Snippet}";
                if (!HasAnyKeywordOverlap(userText, combined))
                    issues.Add($"Anchor '{anchor.Title}' appears unrelated to user request.");
            }

            return issues;
        }

        private static bool HasAnyKeywordOverlap(string a, string b)
        {
            var left = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 3).ToHashSet();
            var right = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 3).ToHashSet();
            return left.Overlaps(right);
        }
    }

    public static class AnswerCompletenessCheck
    {
        public static string? Evaluate(string userText, string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "Response is empty.";

            var evasive = new[] { "can't help", "cannot help", "no comment" };
            if (evasive.Any(e => response.Contains(e, StringComparison.OrdinalIgnoreCase)))
                return "Response is evasive.";

            var userKeywords = userText.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(k => k.Length > 3)
                .Select(k => k.ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (userKeywords.Length == 0) return null;

            var covered = userKeywords.Count(k => response.Contains(k, StringComparison.OrdinalIgnoreCase));
            if (covered < Math.Max(1, userKeywords.Length / 4))
                return "Response does not sufficiently address the prompt.";

            return null;
        }
    }

    public static class ClaimSanityCheck
    {
        public static string? Evaluate(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
                return "Response is empty.";

            if (response.Length < 8)
                return "Response is too short to be useful.";

            return null;
        }
    }

    // IQualityCheck wrappers

    public sealed class ClaimSanityQualityCheck : IQualityCheck
    {
        public string Name => nameof(ClaimSanityQualityCheck);

        public Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct)
        {
            var finding = ClaimSanityCheck.Evaluate(input.DraftAnswer);
            return Task.FromResult(finding == null
                ? new QualityCheckResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
                : new QualityCheckResult(false, new[] { finding }, Array.Empty<string>(), Array.Empty<string>()));
        }
    }

    public sealed class NoPhantomCitationsQualityCheck : IQualityCheck
    {
        public string Name => nameof(NoPhantomCitationsQualityCheck);

        public Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct)
        {
            var finding = NoPhantomCitationsCheck.Evaluate(input.DraftAnswer, input.AnchorsPi);
            return Task.FromResult(ToResult(finding));
        }

        private static QualityCheckResult ToResult(string? finding)
        {
            if (finding != null)
                System.Diagnostics.Debug.WriteLine($"[NoPhantomCitations] Finding: {finding}");

            return finding == null
                ? new QualityCheckResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
                : new QualityCheckResult(false, new[] { finding }, Array.Empty<string>(), Array.Empty<string>());
        }
    }

    public sealed class AnswerCompletenessQualityCheck : IQualityCheck
    {
        public string Name => nameof(AnswerCompletenessQualityCheck);

        public Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct)
        {
            var finding = AnswerCompletenessCheck.Evaluate(input.UserText, input.DraftAnswer);

            if (finding != null)
                System.Diagnostics.Debug.WriteLine($"[AnswerCompleteness] Finding: {finding}");

            return Task.FromResult(finding == null
                ? new QualityCheckResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
                : new QualityCheckResult(false, new[] { finding }, Array.Empty<string>(), Array.Empty<string>()));
        }
    }

    public sealed class GroundingQualityCheck : IQualityCheck
    {
        public string Name => nameof(GroundingQualityCheck);

        public Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct)
        {
            var unsupported = GroundingVerifier.Evaluate(input.DraftAnswer, input.AnchorsPi);

            if (unsupported.Count > 0)
                System.Diagnostics.Debug.WriteLine($"[Grounding] Unsupported claims: {unsupported.Count}");

            return Task.FromResult(unsupported.Count == 0
                ? new QualityCheckResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
                : new QualityCheckResult(false, Array.Empty<string>(), unsupported, Array.Empty<string>()));
        }
    }

    public sealed class SourceQualityCheck : IQualityCheck
    {
        public string Name => nameof(SourceQualityCheck);

        public Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct)
        {
            var issues = SourceVerifier.Evaluate(input.UserText, input.AnchorsPi);

            if (issues.Count > 0)
                System.Diagnostics.Debug.WriteLine($"[SourceQuality] Issues: {issues.Count}");

            return Task.FromResult(issues.Count == 0
                ? new QualityCheckResult(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>())
                : new QualityCheckResult(false, Array.Empty<string>(), Array.Empty<string>(), issues));
        }
    }
}