using System;
using System.Collections.Generic;

namespace SageRage.Grounding;

/// <summary>
/// Retrieval / trusted-source hit. Only minting paths allowed by policy may create these.
/// Actor free-text must not invent IDs that pass the gate.
/// </summary>
public sealed record EvidenceHit(
    string Id,
    string Payload,
    float Score,
    string Source = "retriever");

/// <summary>
/// Tool request that depends on grounded evidence.
/// Claim-like content is bound to an evidence ID, not free invention.
/// </summary>
public sealed record GroundedToolRequest(
    string ToolName,
    string Assertion,
    string? EvidenceId,
    ToolBlastRadius BlastRadius = ToolBlastRadius.High);

public enum ToolBlastRadius
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ToolDenyReason
{
    Allowed,
    MissingEvidenceId,
    EvidenceNotInAllowed,
    PoolEmptyOrThin,
    EntailmentFailed,
    MintNotTrusted
}

public sealed record ToolDenyDecision(
    bool Allowed,
    ToolDenyReason Reason,
    string Detail = "");

/// <summary>
/// Thresholds and mint policy. Changes are amendment-class events in a real deploy.
/// </summary>
public sealed class GroundingPolicy
{
    /// <summary>Minimum retrieval score to enter Allowed for high-blast tools.</summary>
    public float TauHigh { get; init; } = 0.75f;

    /// <summary>Minimum score for low-blast tools.</summary>
    public float TauLow { get; init; } = 0.40f;

    /// <summary>Max hits below this count with no score ≥ tau still treated as thin for high-blast.</summary>
    public int ThinPoolMaxHits { get; init; } = 3;

    /// <summary>If true, high-blast tools require entailment(payload, assertion).</summary>
    public bool RequireEntailmentForHighBlast { get; init; } = true;

    /// <summary>Sources allowed to mint evidence into Allowed.</summary>
    public HashSet<string> TrustedMintSources { get; init; } =
        new(StringComparer.OrdinalIgnoreCase) { "retriever", "human" };
}
