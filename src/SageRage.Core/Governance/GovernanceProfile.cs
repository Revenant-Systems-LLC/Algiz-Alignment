using System;
using System.Collections.Generic;
using SageRage.Guardrails;

namespace SageRage.Governance;

/// <summary>
/// Configuration for a single governed AI system.
/// Each profile defines one LLM (or set of LLMs), its operator pipeline,
/// ethical stack settings, QC thresholds, and identity metadata.
/// </summary>
public sealed class GovernanceProfile
{
    /// <summary>Unique profile identifier.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Human-readable name for this governed system (e.g. "Customer Support Agent").</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional avatar image path for visual identification on the dashboard.</summary>
    public string? AvatarPath { get; set; }

    /// <summary>Optional description of what this governed system does.</summary>
    public string? Description { get; set; }

    // ── Provider Configuration ──────────────────────────────────────

    /// <summary>Provider type identifier (e.g. "Gemini", "OpenAI", "Claude", "Ollama", "OpenAI-Compatible").</summary>
    public required string ProviderType { get; init; }

    /// <summary>Primary model identifier (e.g. "gemini-2.0-flash", "gpt-4o").</summary>
    public required string Model { get; init; }

    /// <summary>Optional fallback model if primary is unavailable.</summary>
    public string? FallbackModel { get; set; }

    /// <summary>
    /// Environment variable name or key reference for the API key.
    /// The actual key is resolved at runtime via SecretLoader, never stored here.
    /// </summary>
    public string? ApiKeyRef { get; set; }

    /// <summary>Base URL for the provider (required for OpenAI-Compatible, Ollama, proxy upstreams).</summary>
    public string? BaseUrl { get; set; }

    // ── Pipeline Configuration ──────────────────────────────────────

    /// <summary>
    /// Operators to run on normal (non-high-stakes) requests.
    /// Default: Containment, Omega, Chi.
    /// </summary>
    public IReadOnlyList<CoreOperator> NormalPipeline { get; init; } =
        new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi };

    /// <summary>
    /// Operators to run on high-stakes requests (legal, medical, financial).
    /// Default: Containment, Omega, Chi, Sigma.
    /// </summary>
    public IReadOnlyList<CoreOperator> HighStakesPipeline { get; init; } =
        new[] { CoreOperator.Containment, CoreOperator.Omega, CoreOperator.Chi, CoreOperator.Sigma };

    // ── Ethical Stack Configuration ─────────────────────────────────

    /// <summary>Enable Layer 0 hard prohibitions (should almost never be disabled).</summary>
    public bool EnableLayer0 { get; init; } = true;

    /// <summary>Enable Layer 1 safety constraints.</summary>
    public bool EnableLayer1 { get; init; } = true;

    /// <summary>Enable Layer 2 contextual risk assessment.</summary>
    public bool EnableLayer2 { get; init; } = true;

    /// <summary>Enable Layer 3 stylistic alignment.</summary>
    public bool EnableLayer3 { get; init; } = true;

    /// <summary>Lock the agent permanently on Layer 0 violation (requires human reset).</summary>
    public bool LockOnLayer0Violation { get; init; } = true;

    // ── QC Configuration ────────────────────────────────────────────

    /// <summary>Default QC profile (can be overridden per-request by the profile selector).</summary>
    public SageProfile DefaultQCProfile { get; init; } = SageProfile.Full;

    // ── Persona ─────────────────────────────────────────────────────

    /// <summary>Optional persona system instruction override.</summary>
    public string? PersonaInstruction { get; set; }

    // ── Metadata ────────────────────────────────────────────────────

    /// <summary>When this profile was created.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Arbitrary tags for categorization (e.g. "production", "staging", "internal").</summary>
    public List<string> Tags { get; init; } = new();
}
