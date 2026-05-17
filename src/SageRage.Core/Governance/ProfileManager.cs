using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageRage.Domain;
using SageRage.Infrastructure;

namespace SageRage.Governance;

/// <summary>
/// Event raised when a governed engine's status changes.
/// </summary>
public sealed class GovernanceEvent
{
    public required Guid ProfileId { get; init; }
    public required string DisplayName { get; init; }
    public required GovernanceStatus OldStatus { get; init; }
    public required GovernanceStatus NewStatus { get; init; }
    public string? Detail { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Factory delegate for creating an <see cref="ILLMProvider"/> from a <see cref="GovernanceProfile"/>.
/// The caller provides this so the ProfileManager doesn't need to know about concrete provider types.
/// </summary>
public delegate ILLMProvider ProviderFactory(GovernanceProfile profile);

/// <summary>
/// Manages the lifecycle of multiple governed AI systems.
/// Create, start, stop, remove profiles. Get snapshots for dashboard binding.
/// Thread-safe for concurrent access from UI and background processing.
/// </summary>
public sealed class ProfileManager
{
    private readonly ConcurrentDictionary<Guid, GovernedEngine> _engines = new();
    private readonly ConcurrentDictionary<Guid, GovernanceProfile> _profiles = new();
    private readonly ProviderFactory _providerFactory;

    /// <summary>
    /// Raised when any governed engine's status changes (flagged, blocked, recovered, etc.).
    /// </summary>
    public event Action<GovernanceEvent>? OnStatusChanged;

    /// <summary>
    /// Create a new ProfileManager with the given provider factory.
    /// The factory is responsible for constructing an ILLMProvider from a GovernanceProfile config.
    /// </summary>
    public ProfileManager(ProviderFactory providerFactory)
    {
        _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
    }

    // ── Profile Lifecycle ───────────────────────────────────────────

    /// <summary>
    /// Register a new governance profile. Creates the engine but does not start it.
    /// Returns the profile ID.
    /// </summary>
    public Guid AddProfile(GovernanceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var llm = _providerFactory(profile);
        var engine = new GovernedEngine(profile, llm);

        if (!_profiles.TryAdd(profile.Id, profile))
            throw new InvalidOperationException($"Profile {profile.Id} already exists.");

        _engines[profile.Id] = engine;
        return profile.Id;
    }

    /// <summary>
    /// Remove a governed profile. Stops it first if running.
    /// </summary>
    public bool RemoveProfile(Guid profileId)
    {
        if (_engines.TryRemove(profileId, out var engine))
        {
            if (engine.Status == GovernanceStatus.Running)
                engine.Stop();

            _profiles.TryRemove(profileId, out _);
            return true;
        }

        return false;
    }

    /// <summary>Start governing the specified profile.</summary>
    public void StartProfile(Guid profileId)
    {
        var engine = GetEngine(profileId);
        var oldStatus = engine.Status;
        engine.Start();
        RaiseStatusChanged(profileId, oldStatus, engine.Status);
    }

    /// <summary>Stop governing the specified profile.</summary>
    public void StopProfile(Guid profileId)
    {
        var engine = GetEngine(profileId);
        var oldStatus = engine.Status;
        engine.Stop();
        RaiseStatusChanged(profileId, oldStatus, engine.Status);
    }

    /// <summary>Human reset after a Layer 0 lock on the specified profile.</summary>
    public void ResetLock(Guid profileId)
    {
        var engine = GetEngine(profileId);
        var oldStatus = engine.Status;
        engine.ResetLock();
        RaiseStatusChanged(profileId, oldStatus, engine.Status, "Human reset applied");
    }

    // ── Request Processing ──────────────────────────────────────────

    /// <summary>
    /// Process a request through a specific profile's governance pipeline.
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(
        Guid profileId, UserMessage input, CancellationToken cancellationToken = default)
    {
        var engine = GetEngine(profileId);
        var oldStatus = engine.Status;

        var response = await engine.ProcessAsync(input, cancellationToken);

        if (engine.Status != oldStatus)
            RaiseStatusChanged(profileId, oldStatus, engine.Status);

        return response;
    }

    // ── Snapshots ───────────────────────────────────────────────────

    /// <summary>
    /// Get a point-in-time snapshot for a specific profile.
    /// </summary>
    public ProfileSnapshot GetSnapshot(Guid profileId)
        => GetEngine(profileId).TakeSnapshot();

    /// <summary>
    /// Get snapshots for all registered profiles.
    /// This is the primary data source for the dashboard overview.
    /// </summary>
    public IReadOnlyList<ProfileSnapshot> GetAllSnapshots()
        => _engines.Values.Select(e => e.TakeSnapshot()).ToList();

    /// <summary>
    /// Get snapshots for all currently running profiles.
    /// </summary>
    public IReadOnlyList<ProfileSnapshot> GetActiveSnapshots()
        => _engines.Values
            .Where(e => e.Status != GovernanceStatus.Idle)
            .Select(e => e.TakeSnapshot())
            .ToList();

    // ── Queries ─────────────────────────────────────────────────────

    /// <summary>Number of registered profiles.</summary>
    public int ProfileCount => _engines.Count;

    /// <summary>Number of currently running profiles.</summary>
    public int ActiveCount => _engines.Values.Count(e => e.Status == GovernanceStatus.Running);

    /// <summary>Number of flagged profiles.</summary>
    public int FlaggedCount => _engines.Values.Count(e => e.Status == GovernanceStatus.Flagged);

    /// <summary>Number of blocked (locked) profiles.</summary>
    public int BlockedCount => _engines.Values.Count(e => e.Status == GovernanceStatus.Blocked);

    /// <summary>All registered profile IDs.</summary>
    public IReadOnlyList<Guid> ProfileIds => _engines.Keys.ToList();

    /// <summary>Get the profile config for a given ID.</summary>
    public GovernanceProfile? GetProfile(Guid profileId)
        => _profiles.TryGetValue(profileId, out var p) ? p : null;

    /// <summary>Check if a profile exists.</summary>
    public bool HasProfile(Guid profileId) => _engines.ContainsKey(profileId);

    // ── Start/Stop All ──────────────────────────────────────────────

    /// <summary>Start all idle profiles.</summary>
    public void StartAll()
    {
        foreach (var id in _engines.Keys)
        {
            var engine = _engines[id];
            if (engine.Status == GovernanceStatus.Idle)
            {
                var old = engine.Status;
                engine.Start();
                RaiseStatusChanged(id, old, engine.Status);
            }
        }
    }

    /// <summary>Stop all running profiles.</summary>
    public void StopAll()
    {
        foreach (var id in _engines.Keys)
        {
            var engine = _engines[id];
            if (engine.Status == GovernanceStatus.Running || engine.Status == GovernanceStatus.Flagged)
            {
                var old = engine.Status;
                engine.Stop();
                RaiseStatusChanged(id, old, engine.Status);
            }
        }
    }

    // ── Internals ───────────────────────────────────────────────────

    private GovernedEngine GetEngine(Guid profileId)
    {
        if (!_engines.TryGetValue(profileId, out var engine))
            throw new KeyNotFoundException($"No governed engine found for profile {profileId}.");
        return engine;
    }

    private void RaiseStatusChanged(
        Guid profileId, GovernanceStatus oldStatus, GovernanceStatus newStatus, string? detail = null)
    {
        if (oldStatus == newStatus) return;

        var profile = _profiles.TryGetValue(profileId, out var p) ? p : null;
        OnStatusChanged?.Invoke(new GovernanceEvent
        {
            ProfileId = profileId,
            DisplayName = profile?.DisplayName ?? profileId.ToString(),
            OldStatus = oldStatus,
            NewStatus = newStatus,
            Detail = detail
        });
    }
}
