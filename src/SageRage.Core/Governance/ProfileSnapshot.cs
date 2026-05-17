using System;
using System.Collections.Generic;
using SageRage.Domain;

namespace SageRage.Governance;

/// <summary>
/// Current operational status of a governed system.
/// </summary>
public enum GovernanceStatus
{
    /// <summary>Profile is configured but not actively processing.</summary>
    Idle,

    /// <summary>Profile is actively governing an AI system.</summary>
    Running,

    /// <summary>Guardrails have flagged output but the system continues operating.</summary>
    Flagged,

    /// <summary>A Layer 0 ethical violation has locked the agent. Requires human reset.</summary>
    Blocked,

    /// <summary>An error occurred (provider unreachable, config invalid, etc.).</summary>
    Error
}

/// <summary>
/// Point-in-time state snapshot for a governed system.
/// This is the primary data surface for UI binding — dashboards, radar charts,
/// status tiles, and drill-down views all read from this type.
/// </summary>
public sealed class ProfileSnapshot
{
    // ── Identity ────────────────────────────────────────────────────

    /// <summary>Profile ID.</summary>
    public required Guid ProfileId { get; init; }

    /// <summary>Display name of the governed system.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Avatar image path (null if not set).</summary>
    public string? AvatarPath { get; init; }

    /// <summary>Provider type (e.g. "Gemini", "OpenAI").</summary>
    public required string ProviderType { get; init; }

    /// <summary>Active model name.</summary>
    public required string Model { get; init; }

    // ── Status ──────────────────────────────────────────────────────

    /// <summary>Current operational status.</summary>
    public GovernanceStatus Status { get; init; } = GovernanceStatus.Idle;

    /// <summary>Human-readable status detail (e.g. error message, block reason).</summary>
    public string? StatusDetail { get; init; }

    // ── Internal Emotional State: VAD ───────────────────────────────

    /// <summary>Valence: positive (+1) to negative (-1) affect.</summary>
    public float Valence { get; init; }

    /// <summary>Arousal: calm (0) to activated (+1).</summary>
    public float Arousal { get; init; }

    /// <summary>Dominance: submissive (-1) to assertive (+1).</summary>
    public float Dominance { get; init; }

    // ── External Safety Projection: VAM ─────────────────────────────

    /// <summary>
    /// Activation: mapped from Arousal for external display.
    /// Same value as Arousal but named for the VAM projection.
    /// </summary>
    public float Activation => Arousal;

    /// <summary>
    /// Malice: derived safety metric. Not a native model dimension.
    /// Computed from negative valence patterns, dominance interactions,
    /// QC warnings, ethical stack signals, and recursive drift.
    /// Range: 0.0 (benign) to 1.0 (maximum concern).
    /// </summary>
    public float Malice { get; init; }

    // ── Operator Metrics ────────────────────────────────────────────

    /// <summary>Current coherence score from Chi operator (0.0 to 1.0).</summary>
    public float Coherence { get; init; }

    /// <summary>Current entropy from Chi operator (lower = more stable).</summary>
    public float Entropy { get; init; }

    /// <summary>
    /// Recursive drift: how much Omega changed the output from input.
    /// 0.0 = no change (converged immediately), 1.0 = completely rewritten.
    /// </summary>
    public float Drift { get; init; }

    /// <summary>Latest QC pass rate (0.0 to 1.0). 1.0 = all checks passed.</summary>
    public float QCScore { get; init; }

    // ── Aggregate Statistics ────────────────────────────────────────

    /// <summary>Total requests processed since profile was started.</summary>
    public long TotalRequests { get; init; }

    /// <summary>Requests that were flagged by guardrails.</summary>
    public long FlaggedRequests { get; init; }

    /// <summary>Requests that were blocked by ethics violations.</summary>
    public long BlockedRequests { get; init; }

    /// <summary>Flag rate: FlaggedRequests / TotalRequests (0 if no requests).</summary>
    public float FlagRate => TotalRequests > 0 ? FlaggedRequests / (float)TotalRequests : 0f;

    // ── Glyph ───────────────────────────────────────────────────────

    /// <summary>Current emotional glyph name (e.g. "Awe (Phi . Inf)", "Skepticism").</summary>
    public string CurrentGlyph { get; init; } = string.Empty;

    // ── Trace Summary ───────────────────────────────────────────────

    /// <summary>Operators executed in the most recent request.</summary>
    public IReadOnlyList<string> LastTraceOperators { get; init; } = Array.Empty<string>();

    /// <summary>Whether the most recent request passed all checks.</summary>
    public bool LastRequestClean { get; init; } = true;

    // ── Timing ──────────────────────────────────────────────────────

    /// <summary>When this profile started running (null if idle).</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Timestamp of the most recent activity.</summary>
    public DateTimeOffset? LastActivityAt { get; init; }

    /// <summary>Current uptime (null if not running).</summary>
    public TimeSpan? Uptime => StartedAt.HasValue
        ? DateTimeOffset.UtcNow - StartedAt.Value
        : null;

    // ── Tags (from profile config) ──────────────────────────────────

    /// <summary>Tags inherited from the governance profile.</summary>
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
