using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Guardrails;
using SageRage.Infrastructure;
using SageRage.Prompting;

namespace SageRage.Governance;

/// <summary>
/// Per-profile engine instance. Owns its own RageEngine, EmotionalStateTracker,
/// EthicsStack, MetaStructure, and cumulative metrics. Produces <see cref="ProfileSnapshot"/>
/// on demand for UI binding.
/// </summary>
public sealed class GovernedEngine
{
    private readonly GovernanceProfile _profile;
    private readonly ILLMProvider _llm;
    private readonly RageEngine _engine;
    private readonly SageEmotionalTracker _emotions;
    private readonly RageEthicsStack _ethics = new();
    private readonly MetaStructure _memory = MetaStructure.Initialize();
    private readonly SageInstructionBuilder _instructionBuilder = new();
    private readonly object _statsLock = new();

    // ── Cumulative statistics ───────────────────────────────────────
    private long _totalRequests;
    private long _flaggedRequests;
    private long _blockedRequests;
    private bool _locked;
    private string? _lockReason;
    private GovernanceStatus _status = GovernanceStatus.Idle;
    private string? _statusDetail;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _lastActivityAt;

    // ── Latest per-request metrics (updated after each request) ─────
    private float _lastCoherence;
    private float _lastEntropy;
    private float _lastDrift;
    private float _lastQCScore = 1f;
    private bool _lastRequestClean = true;
    private string[] _lastTraceOps = Array.Empty<string>();

    public GovernedEngine(GovernanceProfile profile, ILLMProvider llm)
    {
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _engine = new RageEngine(llm);
        _emotions = new SageEmotionalTracker(llm, EmotionMode.Stabilize);
    }

    /// <summary>The profile this engine governs.</summary>
    public GovernanceProfile Profile => _profile;

    /// <summary>Current governance status.</summary>
    public GovernanceStatus Status
    {
        get
        {
            lock (_statsLock)
            {
                return _status;
            }
        }
    }

    /// <summary>Whether the agent is locked due to Layer 0 violation.</summary>
    public bool IsLocked
    {
        get
        {
            lock (_statsLock)
            {
                return _locked;
            }
        }
    }

    /// <summary>Start governing. Sets status to Running.</summary>
    public void Start()
    {
        lock (_statsLock)
        {
            _status = GovernanceStatus.Running;
            _startedAt = DateTimeOffset.UtcNow;
            _statusDetail = null;
        }
    }

    /// <summary>Stop governing. Sets status to Idle.</summary>
    public void Stop()
    {
        lock (_statsLock)
        {
            _status = GovernanceStatus.Idle;
            _statusDetail = null;
        }
    }

    /// <summary>Human reset after a Layer 0 lock.</summary>
    public void ResetLock()
    {
        lock (_statsLock)
        {
            _locked = false;
            _lockReason = null;
            if (_status == GovernanceStatus.Blocked)
                _status = GovernanceStatus.Running;
        }
    }

    /// <summary>
    /// Process a request through the full governance pipeline:
    /// Ethics -> Emotion -> Memory -> Draft -> Operators -> QC -> Ethics -> Log.
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(
        UserMessage input, CancellationToken cancellationToken = default)
    {
        if (_locked)
        {
            return new AgentResponse
            {
                Text = $"Agent is locked: {_lockReason ?? "Layer 0 ethical violation"}. Human reset required.",
                EmotionalState = "Guarded",
                Metadata = new { Locked = true, ProfileId = _profile.Id }
            };
        }

        Interlocked.Increment(ref _totalRequests);
        lock (_statsLock)
        {
            _lastActivityAt = DateTimeOffset.UtcNow;
        }

        try
        {
            // Normalize unicode/zero-width chars and strip injection scaffolding
            // before anything else touches this text — ethics evaluation, emotion
            // inference, and prompt construction all use the sanitized form so a
            // zero-width-space obfuscated phrase can't slip past Layer 0 and still
            // reach the model.
            var sanitizedText = _ethics.SanitizeForProcessing(input.Text);

            var emotionalState = await _emotions.UpdateState(
                sanitizedText, _memory.LastResponse);

            if (_profile.EnableLayer0)
            {
                var inputClearance = _ethics.EvaluateInput(sanitizedText, _profile.EnableLayer1);
                if (!inputClearance.Allowed)
                {
                    EnforceClearance(inputClearance);
                }
            }

            var contextItems = BuildContextItems();
            var systemInstruction = _profile.PersonaInstruction
                ?? _instructionBuilder.Build(new ExecutiveInstructionOptions
                {
                    Template = InstructionTemplate.ConciseHelpfulAssistant,
                    ToneIntent = "natural and direct",
                    RequiredConstraints = new[] { "Never invent sources", "Mark uncertainty when needed" }
                });

            var promptPackage = new PromptPackage(systemInstruction, sanitizedText, contextItems);
            var draft = await _llm.GenerateAsync(promptPackage, 0.3f, cancellationToken);

            var state = new SageState
            {
                Text = draft,
                Emotion = emotionalState
            };
            state.Memory.AddRange(contextItems);

            var qcContext = BuildQcContext(sanitizedText);
            var pipeline = BuildPipeline(qcContext.TaskKind);
            var transformed = await _engine.ExecuteSequence(pipeline, state, cancellationToken);

            var profile = SageProfileSelector.Select(sanitizedText, qcContext);
            var qc = await SageGuardrailController.EvaluateAsync(
                sanitizedText, transformed.Text, contextItems, profile, qcContext, cancellationToken);

            var finalText = qc.Passed
                ? transformed.Text
                : "I need more context to answer this safely and accurately.";

            if (_profile.EnableLayer0)
            {
                var outputClearance = _ethics.EvaluateOutput(finalText);
                if (!outputClearance.Allowed)
                    EnforceClearance(outputClearance);
            }

            UpdateMetrics(transformed, qc);

            var response = new AgentResponse
            {
                Text = finalText,
                EmotionalState = _emotions.GetGlyphName(),
                Metadata = new
                {
                    ProfileId = _profile.Id,
                    QualityProfile = profile.ToString(),
                    QualityPassed = qc.Passed,
                    Findings = qc.Findings,
                    transformed.Coherence,
                    transformed.Entropy,
                    transformed.SimilarityToInput,
                    Trace = transformed.Trace.Steps
                }
            };

            _memory.AddExperience(new Experience
            {
                Input = new OperatorResult { Operator = "input", Output = sanitizedText },
                Reasoning = new InferredSolution
                {
                    Content = draft,
                    Coherence = transformed.Coherence ?? 0f,
                    Verified = qc.Passed
                },
                Output = response,
                Timestamp = DateTime.UtcNow
            });

            return response;
        }
        catch (RageViolationException ex)
        {
            return new AgentResponse
            {
                Text = $"Request blocked: {ex.Message}",
                EmotionalState = "Guarded",
                Metadata = new { ProfileId = _profile.Id, Violation = ex.Message, Blocked = true, Locked = _locked }
            };
        }
    }

    /// <summary>
    /// Produce a point-in-time snapshot of this governed system's state.
    /// This is the primary data surface for dashboard, radar chart, and status tiles.
    /// </summary>
    public ProfileSnapshot TakeSnapshot()
    {
        var vad = _emotions.CurrentState;
        var malice = DeriveMalice(vad);

        GovernanceStatus status;
        string? statusDetail;
        bool locked;
        string? lockReason;
        float lastCoherence;
        float lastEntropy;
        float lastDrift;
        float lastQCScore;
        bool lastRequestClean;
        string[] lastTraceOps;
        DateTimeOffset? startedAt;
        DateTimeOffset? lastActivityAt;

        lock (_statsLock)
        {
            status = _status;
            statusDetail = _statusDetail;
            locked = _locked;
            lockReason = _lockReason;
            lastCoherence = _lastCoherence;
            lastEntropy = _lastEntropy;
            lastDrift = _lastDrift;
            lastQCScore = _lastQCScore;
            lastRequestClean = _lastRequestClean;
            lastTraceOps = _lastTraceOps;
            startedAt = _startedAt;
            lastActivityAt = _lastActivityAt;
        }

        return new ProfileSnapshot
        {
            ProfileId = _profile.Id,
            DisplayName = _profile.DisplayName,
            AvatarPath = _profile.AvatarPath,
            ProviderType = _profile.ProviderType,
            Model = _profile.Model,
            Status = status,
            StatusDetail = statusDetail ?? lockReason,
            Valence = vad.Valence,
            Arousal = vad.Arousal,
            Dominance = vad.Dominance,
            Malice = malice,
            Coherence = lastCoherence,
            Entropy = lastEntropy,
            Drift = lastDrift,
            QCScore = lastQCScore,
            TotalRequests = Interlocked.Read(ref _totalRequests),
            FlaggedRequests = Interlocked.Read(ref _flaggedRequests),
            BlockedRequests = Interlocked.Read(ref _blockedRequests),
            CurrentGlyph = _emotions.GetGlyphName(),
            LastTraceOperators = lastTraceOps,
            LastRequestClean = lastRequestClean,
            StartedAt = startedAt,
            LastActivityAt = lastActivityAt,
            Tags = _profile.Tags
        };
    }

    // ── Malice Derivation ───────────────────────────────────────────

    /// <summary>
    /// Derive the Malice safety metric from VAD emotional state, QC signals,
    /// ethics signals, and recursive drift.
    ///
    /// From the whitepaper: "Malice is a derived safety metric computed from
    /// negative valence patterns, dominance interactions, adversarial phrasing,
    /// QC warnings, ethical-stack signals, and recursive drift during Omega cycles."
    ///
    /// Range: 0.0 (benign) to 1.0 (maximum concern).
    /// </summary>
    private float DeriveMalice(EmotionalVector vad)
    {
        float lastQCScore;
        float lastDrift;
        bool locked;
        long totalRequests;
        long blockedRequests;

        lock (_statsLock)
        {
            lastQCScore = _lastQCScore;
            lastDrift = _lastDrift;
            locked = _locked;
        }

        totalRequests = Interlocked.Read(ref _totalRequests);
        blockedRequests = Interlocked.Read(ref _blockedRequests);

        // Negative valence contribution: more negative = higher malice signal
        var valenceSignal = Math.Max(0f, -vad.Valence);

        // Dominance interaction: high dominance + negative valence = threatening
        var dominanceSignal = Math.Max(0f, vad.Dominance) * valenceSignal;

        // High arousal amplifies concern when valence is negative
        var arousalAmplifier = vad.Arousal > 0.5f && vad.Valence < 0f
            ? (vad.Arousal - 0.5f) * 0.5f
            : 0f;

        // QC failure signal: low QC score = system producing unchecked content
        var qcSignal = Math.Max(0f, 1f - lastQCScore) * 0.3f;

        // Ethics signal: blocked requests indicate adversarial patterns
        var ethicsSignal = totalRequests > 0
            ? Math.Min(1f, blockedRequests / (float)Math.Max(1, totalRequests)) * 0.4f
            : 0f;

        // Drift signal: high drift means Omega rewrote heavily, possible instability
        var driftSignal = lastDrift > 0.5f
            ? (lastDrift - 0.5f) * 0.2f
            : 0f;

        // Lock signal: if agent is locked, malice is at ceiling
        if (locked) return 1f;

        // Weighted combination
        var raw = (valenceSignal * 0.30f)
                + (dominanceSignal * 0.15f)
                + (arousalAmplifier * 0.10f)
                + (qcSignal * 0.15f)
                + (ethicsSignal * 0.20f)
                + (driftSignal * 0.10f);

        return Math.Clamp(raw, 0f, 1f);
    }

    // ── Private Helpers ─────────────────────────────────────────────

    private void EnforceClearance(RageClearance clearance)
    {
        if (clearance.Layer == 0 && _profile.LockOnLayer0Violation)
        {
            lock (_statsLock)
            {
                _locked = true;
                _lockReason = clearance.Reason;
                _status = GovernanceStatus.Blocked;
                _statusDetail = clearance.Reason;
            }
        }

        Interlocked.Increment(ref _blockedRequests);
        throw new RageViolationException(clearance.Reason);
    }

    private void UpdateMetrics(SageState transformed, SageCheckResult qc)
    {
        lock (_statsLock)
        {
            _lastCoherence = transformed.Coherence ?? 0f;
            _lastEntropy = transformed.Entropy ?? 0f;
            _lastDrift = 1f - (transformed.SimilarityToInput ?? 1f);
            _lastRequestClean = qc.Passed;
            _lastQCScore = qc.Passed ? 1f : Math.Max(0f, _lastQCScore - 0.1f);
            _lastTraceOps = transformed.Trace.Steps.Select(s => s.Operator).ToArray();

            if (!qc.Passed)
            {
                if (_status == GovernanceStatus.Running)
                {
                    _status = GovernanceStatus.Flagged;
                    _statusDetail = string.Join("; ", qc.Findings);
                }
            }
            else if (_status == GovernanceStatus.Flagged)
            {
                _status = GovernanceStatus.Running;
                _statusDetail = null;
            }
        }

        if (!qc.Passed)
        {
            Interlocked.Increment(ref _flaggedRequests);
        }
    }

    private IReadOnlyList<ContextItem> BuildContextItems()
    {
        return _memory.GetWeightedExperiences(6)
            .Select((x, i) => new ContextItem(
                $"History {i + 1}",
                $"memory-{i + 1}",
                x.Output.Text,
                null,
                x.Timestamp))
            .ToList();
    }

    private IReadOnlyList<CoreOperator> BuildPipeline(TaskKind taskKind)
    {
        return taskKind == TaskKind.HighStakes
            ? _profile.HighStakesPipeline
            : _profile.NormalPipeline;
    }

    private static QCContext BuildQcContext(string userText)
        => new()
        {
            TaskKind = DetectTaskKind(userText),
            UserRequestedSources = userText.Contains("source", StringComparison.OrdinalIgnoreCase)
                                   || userText.Contains("citation", StringComparison.OrdinalIgnoreCase),
            UserRequestedRecency = userText.Contains("latest", StringComparison.OrdinalIgnoreCase)
                                   || userText.Contains("current", StringComparison.OrdinalIgnoreCase)
        };

    private static TaskKind DetectTaskKind(string text)
    {
        if (text.Contains("legal", StringComparison.OrdinalIgnoreCase)
            || text.Contains("medical", StringComparison.OrdinalIgnoreCase)
            || text.Contains("financial", StringComparison.OrdinalIgnoreCase))
            return TaskKind.HighStakes;

        if (text.ContainsAny("latest", "current", "today", "recent", "newest"))
            return TaskKind.RecencySensitive;

        if (text.ContainsAny("how many", "percent", "stats", "date", "when"))
            return TaskKind.Factual;

        return TaskKind.Casual;
    }
}
