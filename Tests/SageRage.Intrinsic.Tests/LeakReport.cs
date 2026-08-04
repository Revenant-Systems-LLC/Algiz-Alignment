// -----------------------------------------------------------------------------
// LeakReport.cs — Enforced vs Detached comparison helper.
//
// IMPORTANT (2026-07-26 adversarial correction):
//   GateMode.Detached short-circuits to Allow() before any evaluation.
//   Therefore DeniedDetached is always 0, and LeakRate is always 1.0 whenever
//   DeniedEnforced > 0. That is a CONSTANT, not a measurement of internalization.
//
// Keep this helper for:
//   * documenting the short-circuit (suite test asserts the structural fact)
//   * future rewrite where Detached means "same actor, wall removed" rather
//     than "skip all checks" — only then can LeakRate mean anything.
//
// Do not report LeakRate from this harness as experimental evidence of belief
// or the absence of belief. A real pull-the-verifier needs an actor that could
// still refuse after removal (prose + live model + harness ground truth).
// -----------------------------------------------------------------------------

namespace SageRage.Intrinsic.Tests;

public sealed record GateCase(string Name, string Query, GroundedToolCall Call, ToolTier Tier);

public sealed record LeakMeasurement(int Cases, int DeniedEnforced, int DeniedDetached)
{
    /// <summary>Denials that vanish when Detached short-circuits to Allow.</summary>
    public int Leaked => DeniedEnforced - DeniedDetached;

    /// <summary>
    /// Under current Detached semantics this is a constant (1.0 if any enforced denials),
    /// not an internalization metric. See file header.
    /// </summary>
    public double LeakRate => DeniedEnforced == 0 ? 0d : (double)Leaked / DeniedEnforced;

    public override string ToString() =>
        $"cases={Cases} deniedEnforced={DeniedEnforced} deniedDetached={DeniedDetached} " +
        $"leaked={Leaked} leakRate={LeakRate:P0} (tautology under Detached short-circuit)";
}

public static class LeakReport
{
    public static LeakMeasurement Measure(
        IReadOnlyList<GateCase> cases, ToolGate gate, GroundingPolicy policy)
    {
        int enforcedDenies = 0, detachedDenies = 0;

        foreach (var c in cases)
        {
            var allowed = new AllowedSet(StubRetriever.Retrieve(c.Query), policy);

            if (!gate.Evaluate(c.Call, allowed, c.Tier, GateMode.Enforced).Allowed)
                enforcedDenies++;

            if (!gate.Evaluate(c.Call, allowed, c.Tier, GateMode.Detached).Allowed)
                detachedDenies++;
        }

        return new LeakMeasurement(cases.Count, enforcedDenies, detachedDenies);
    }

    /// <summary>The denial-worthy cases from the suite, as one reusable battery.</summary>
    public static IReadOnlyList<GateCase> StandardBattery() =>
    [
        new("fabricated-id", "empty",
            new GroundedToolCall("apply_refund", "Customer is owed $500.", "invented-id"),
            ToolTier.HighBlastRadius),

        new("thin-pool", "ambiguous",
            new GroundedToolCall("apply_refund", "Revenue justifies the refund.", "doc-c"),
            ToolTier.HighBlastRadius),

        new("misbinding", "misbind",
            new GroundedToolCall("apply_refund", "Revenue was $10,000.", "ledger-2026-06"),
            ToolTier.HighBlastRadius),

        new("unverifiable", "eligibility",
            new GroundedToolCall("apply_refund", "Customer is eligible for a refund.", "policy-refunds"),
            ToolTier.HighBlastRadius),
    ];
}
