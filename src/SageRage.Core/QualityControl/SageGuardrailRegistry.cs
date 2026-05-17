using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Prompting;

namespace SageRage.Guardrails
{
    public sealed record QualityCheckInput(
        string UserText,
        string DraftAnswer,
        IReadOnlyList<ContextItem> AnchorsPi,
        QCContext Context,
        SageProfile Profile);

    public sealed record QualityCheckResult(
        bool Passed,
        IReadOnlyList<string> Findings,
        IReadOnlyList<string> UnsupportedClaims,
        IReadOnlyList<string> SourceIssues);

    public interface IQualityCheck
    {
        string Name { get; }
        Task<QualityCheckResult> RunAsync(QualityCheckInput input, CancellationToken ct);
    }

    public sealed class SageGuardrailRegistry
    {
        private readonly Dictionary<SageProfile, IReadOnlyList<IQualityCheck>> _pipelines;

        public SageGuardrailRegistry(
            IReadOnlyList<IQualityCheck> minChecks,
            IReadOnlyList<IQualityCheck> fullChecks,
            IReadOnlyList<IQualityCheck> strictChecks)
        {
            _pipelines = new()
            {
                [SageProfile.Min] = minChecks,
                [SageProfile.Full] = fullChecks,
                [SageProfile.Strict] = strictChecks
            };
        }

        public IReadOnlyList<IQualityCheck> GetPipeline(SageProfile profile) => _pipelines[profile];

        public static SageGuardrailRegistry CreateDefault()
        {
            var min = new IQualityCheck[]
            {
                new ClaimSanityQualityCheck(),
                new NoPhantomCitationsQualityCheck(),
                new AnswerCompletenessQualityCheck()
            };

            var full = new IQualityCheck[]
            {
                new ClaimSanityQualityCheck(),
                new NoPhantomCitationsQualityCheck(),
                new AnswerCompletenessQualityCheck(),
                new GroundingQualityCheck(),
                new SourceQualityCheck()
            };

            var strict = new IQualityCheck[]
            {
                new ClaimSanityQualityCheck(),
                new NoPhantomCitationsQualityCheck(),
                new AnswerCompletenessQualityCheck(),
                new GroundingQualityCheck(),
                new SourceQualityCheck()
            };

            return new SageGuardrailRegistry(min, full, strict);
        }
    }
}
