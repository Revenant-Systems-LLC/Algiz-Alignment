using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Guardrails
{
    public static class SageGuardrailController
    {
        public static SageCheckResult Evaluate(
            string userText,
            string response,
            IReadOnlyList<ContextItem> anchors,
            SageProfile profile,
            QCContext context)
            => EvaluateAsync(userText, response, anchors, profile, context, SageGuardrailRegistry.CreateDefault(), CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        public static async Task<SageCheckResult> EvaluateAsync(
            string userText,
            string response,
            IReadOnlyList<ContextItem> anchors,
            SageProfile profile,
            QCContext context,
            SageGuardrailRegistry registry,
            CancellationToken cancellationToken)
        {
            var findings = new List<string>();
            var unsupportedClaims = new List<string>();
            var sourceIssues = new List<string>();

            var input = new QualityCheckInput(userText, response, anchors, context, profile);
            foreach (var check in registry.GetPipeline(profile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await check.RunAsync(input, cancellationToken);
                if (!result.Passed)
                {
                    findings.AddRange(result.Findings);
                    unsupportedClaims.AddRange(result.UnsupportedClaims);
                    sourceIssues.AddRange(result.SourceIssues);
                }
            }

            var passed = profile switch
            {
                SageProfile.Strict => findings.Count == 0 && unsupportedClaims.Count == 0 && sourceIssues.Count == 0,
                SageProfile.Full => EvaluateFullPolicy(findings, unsupportedClaims, anchors, context),
                _ => findings.Count == 0
            };

            return new SageCheckResult
            {
                Passed = passed,
                Findings = findings,
                UnsupportedClaims = unsupportedClaims,
                SourceIssues = sourceIssues
            };
        }

        private static bool EvaluateFullPolicy(
            List<string> findings,
            List<string> unsupportedClaims,
            IReadOnlyList<ContextItem> anchors,
            QCContext context)
        {
            if (findings.Count > 0)
                return false;

            if (anchors.Count > 0 && unsupportedClaims.Count > 0)
                return false;

            if (context.TaskKind == TaskKind.Factual && anchors.Count == 0 &&
                (context.UserRequestedSources || context.UserRequestedRecency))
                return false;

            return true;
        }
    }
}
