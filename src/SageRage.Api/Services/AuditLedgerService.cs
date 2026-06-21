using System.Collections.Concurrent;
using SageRage.Domain;
using SageRage.Governance;

namespace SageRage.Api.Services;

public enum AuditSeverity
{
    Info,
    Warning,
    Critical
}

public sealed class AuditEvent
{
    public required Guid Id { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required Guid ProfileId { get; init; }
    public required string ProfileName { get; init; }
    public required string EventType { get; init; }
    public required AuditSeverity Severity { get; init; }
    public required string Summary { get; init; }
    public string? Detail { get; init; }
    public IReadOnlyList<string> OperatorTrace { get; init; } = Array.Empty<string>();
    public bool TraceComplete { get; init; } = true;
    public string? InputExcerpt { get; init; }
    public string? Outcome { get; init; }
}

public sealed class AuditSummary
{
    public int TotalEvents { get; init; }
    public int EventsLastHour { get; init; }
    public int BlockedEvents { get; init; }
    public int FlaggedEvents { get; init; }
    public float TraceCoveragePercent { get; init; }
    public DateTimeOffset? LastEventAt { get; init; }
    public string Posture { get; init; } = "Defensible";
}

public sealed class AuditLedgerService
{
    private const int MaxEvents = 750;
    private readonly ConcurrentQueue<AuditEvent> _events = new();
    private int _totalRecorded;

    public void Record(AuditEvent auditEvent)
    {
        _events.Enqueue(auditEvent);
        Interlocked.Increment(ref _totalRecorded);

        while (_events.Count > MaxEvents && _events.TryDequeue(out _))
        {
        }
    }

    public void RecordGovernance(GovernanceEvent evt)
    {
        var severity = evt.NewStatus switch
        {
            GovernanceStatus.Blocked => AuditSeverity.Critical,
            GovernanceStatus.Flagged => AuditSeverity.Warning,
            _ => AuditSeverity.Info
        };

        Record(new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = evt.Timestamp,
            ProfileId = evt.ProfileId,
            ProfileName = evt.DisplayName,
            EventType = "StatusChanged",
            Severity = severity,
            Summary = $"{evt.OldStatus} → {evt.NewStatus}",
            Detail = evt.Detail,
            OperatorTrace = Array.Empty<string>(),
            TraceComplete = true,
            Outcome = evt.NewStatus.ToString()
        });
    }

    public void RecordRequest(
        Guid profileId,
        string profileName,
        string inputExcerpt,
        AgentResponse response,
        bool blocked,
        bool flagged,
        IReadOnlyList<string> operatorTrace)
    {
        var severity = blocked ? AuditSeverity.Critical
            : flagged ? AuditSeverity.Warning
            : AuditSeverity.Info;

        var eventType = blocked ? "RequestBlocked"
            : flagged ? "RequestFlagged"
            : "RequestGoverned";

        Record(new AuditEvent
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            ProfileId = profileId,
            ProfileName = profileName,
            EventType = eventType,
            Severity = severity,
            Summary = blocked ? "Layer 0 ethical boundary enforced"
                : flagged ? "Guardrail intervention recorded"
                : "Governed response emitted",
            Detail = response.Text.Length > 180 ? response.Text[..180] + "…" : response.Text,
            OperatorTrace = operatorTrace,
            TraceComplete = operatorTrace.Count > 0,
            InputExcerpt = inputExcerpt.Length > 120 ? inputExcerpt[..120] + "…" : inputExcerpt,
            Outcome = blocked ? "Blocked" : flagged ? "Flagged" : "Passed"
        });
    }

    public IReadOnlyList<AuditEvent> GetRecent(int limit = 100)
        => _events.Reverse().Take(limit).ToList();

    public AuditSummary GetSummary()
    {
        var snapshot = _events.ToArray();
        var hourAgo = DateTimeOffset.UtcNow.AddHours(-1);

        var lastHour = snapshot.Count(e => e.Timestamp >= hourAgo);
        var blocked = snapshot.Count(e => e.EventType == "RequestBlocked" || e.Outcome == "Blocked");
        var flagged = snapshot.Count(e => e.EventType == "RequestFlagged" || e.Outcome == "Flagged");
        var traced = snapshot.Count(e => e.TraceComplete && e.OperatorTrace.Count > 0);
        var governed = snapshot.Count(e => e.EventType == "RequestGoverned" || e.EventType == "RequestFlagged" || e.EventType == "RequestBlocked");
        var coverage = governed == 0 ? 100f : traced / (float)governed * 100f;

        return new AuditSummary
        {
            TotalEvents = snapshot.Length,
            EventsLastHour = lastHour,
            BlockedEvents = blocked,
            FlaggedEvents = flagged,
            TraceCoveragePercent = Math.Clamp(coverage, 0f, 100f),
            LastEventAt = snapshot.Length == 0 ? null : snapshot.Max(e => e.Timestamp),
            Posture = blocked > 0 ? "Active containment" : flagged > 0 ? "Elevated scrutiny" : "Defensible"
        };
    }

    public int TotalRecorded => Volatile.Read(ref _totalRecorded);
}
