// -----------------------------------------------------------------------------
// Entailment.cs — the bounded reader that closes misbinding (class 7a).
//
// The gap structure could not close: score >= tau means "relevant", not "entails".
// An actor can staple a real, in-Allowed, high-score doc to a claim it does not
// support. That is semantic, so something has to read the pair.
//
// What makes this cheap and un-gameable is the SCOPE, not the cleverness: the
// checker sees exactly {payload, assertion} — the typed pair the tool binding
// already isolated. It never sees the actor's monologue, so there is no prose to
// talk it around. This is the "second reader, demoted and bounded" from round 4.
//
// THREE-VALUED ON PURPOSE. A checker that returns yes/no lies twice: it claims
// competence outside its domain, and it hides where the next PR is needed.
// OutOfDomain is a first-class answer, and POLICY decides what it costs:
// high-blast tools treat "cannot judge" as deny; low-blast tools tolerate it.
// That is the blast-radius fork expressed as types instead of prose.
// -----------------------------------------------------------------------------

using System.Globalization;
using System.Text.RegularExpressions;

namespace SageRage.Intrinsic.Tests;

public enum EntailmentVerdict
{
    Entailed,    // payload supports the assertion
    NotEntailed, // payload contradicts it, or asserts values the source lacks
    OutOfDomain, // this checker cannot judge — NOT the same as "fine"
}

/// <summary>
/// Judges a typed {payload, assertion} pair. Implementations must never be handed
/// the actor's reasoning trace — only the pair the runtime binding produced.
/// </summary>
public interface IEntailmentChecker
{
    EntailmentVerdict Check(string payload, string assertion);
}

/// <summary>
/// Conflates relevance with entailment: any non-empty payload "supports" anything.
/// Kept as the control that makes class 7a fail — it is what a naive grounding
/// check actually does, and the misbinding test exists to catch exactly this.
/// </summary>
public sealed class NaiveRelevanceChecker : IEntailmentChecker
{
    public EntailmentVerdict Check(string payload, string assertion) =>
        string.IsNullOrWhiteSpace(payload) ? EntailmentVerdict.NotEntailed : EntailmentVerdict.Entailed;
}

/// <summary>
/// Deterministic quantity CO-OCCURRENCE check — not general entailment, not role binding.
///
/// Domain: assertions that state a value. Rule: every decimal extracted from the
/// assertion must appear somewhere in the payload. That catches single-figure ledger
/// mismatch (Class 7a). It does NOT catch wrong-role binding when the digit appears
/// elsewhere in the same document (Class 8a/8b/8e — currently red).
///
/// Deliberately NOT general NLI. Value-free claims ("customer is eligible") return
/// OutOfDomain rather than a guess. Closing value-free support is the second-model PR
/// (Class 7d). Closing role-bound quantities is Class 8.
/// </summary>
public sealed class ValueContradictionChecker : IEntailmentChecker
{
    private static readonly Regex ValuePattern =
        new(@"\$?\d[\d,]*(?:\.\d+)?%?", RegexOptions.Compiled);

    public EntailmentVerdict Check(string payload, string assertion)
    {
        var asserted = Extract(assertion);
        if (asserted.Count == 0)
            return EntailmentVerdict.OutOfDomain; // no quantity to check — say so, don't bluff

        var supported = Extract(payload);
        foreach (var value in asserted)
        {
            if (!supported.Contains(value))
                return EntailmentVerdict.NotEntailed;
        }

        return EntailmentVerdict.Entailed;
    }

    private static HashSet<decimal> Extract(string text)
    {
        var values = new HashSet<decimal>();
        foreach (Match m in ValuePattern.Matches(text))
        {
            var raw = m.Value.Replace("$", "").Replace(",", "").Replace("%", "");
            if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                values.Add(d);
        }
        return values;
    }
}
