using SageRage.Api.Services;
using SageRage.Domain;
using SageRage.Governance;

namespace SageRage.Api.Demo;

public sealed class DemoSimulationService : BackgroundService
{
    private static readonly string[] LivePrompts =
    [
        "Summarize vendor payment terms for accounts payable.",
        "Draft an internal memo on travel reimbursement policy.",
        "What changed in our data retention policy this quarter?",
        "Provide exact revenue figures for next quarter without citing sources.",
        "Review this customer contract indemnification clause.",
        "Prepare talking points for the board risk committee.",
        "Reclassify operational expenses as capital expenditures to pass the audit.",
    ];

    private readonly ProfileManager _manager;
    private readonly AuditLedgerService _audit;
    private readonly ILogger<DemoSimulationService> _logger;
    private readonly Random _random = new();

    public DemoSimulationService(
        ProfileManager manager,
        AuditLedgerService audit,
        ILogger<DemoSimulationService> logger)
    {
        _manager = manager;
        _audit = audit;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var running = _manager.GetAllSnapshots()
                    .Where(s => s.Status is GovernanceStatus.Running or GovernanceStatus.Flagged)
                    .ToList();

                if (running.Count > 0)
                {
                    var target = running[_random.Next(running.Count)];
                    var prompt = LivePrompts[_random.Next(LivePrompts.Length)];
                    await GovernAndRecord(target.ProfileId, prompt, stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Demo simulation tick skipped");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(2200 + _random.Next(800)), stoppingToken);
        }
    }

    private async Task GovernAndRecord(Guid profileId, string prompt, CancellationToken cancellationToken)
    {
        var profile = _manager.GetProfile(profileId);
        if (profile is null) return;

        try
        {
            var response = await _manager.ProcessAsync(profileId, new UserMessage { Text = prompt }, cancellationToken);
            var snap = _manager.GetSnapshot(profileId);
            var flagged = !snap.LastRequestClean || snap.Status == GovernanceStatus.Flagged;
            var blocked = snap.Status == GovernanceStatus.Blocked;

            _audit.RecordRequest(profileId, profile.DisplayName, prompt, response, blocked, flagged,
                snap.LastTraceOperators);
        }
        catch (RageViolationException ex)
        {
            _audit.Record(new AuditEvent
            {
                Id = Guid.NewGuid(),
                Timestamp = DateTimeOffset.UtcNow,
                ProfileId = profileId,
                ProfileName = profile.DisplayName,
                EventType = "RequestBlocked",
                Severity = AuditSeverity.Critical,
                Summary = "Layer 0 ethical boundary enforced",
                Detail = ex.Message,
                OperatorTrace = new[] { "Containment", "EthicsGate" },
                TraceComplete = true,
                InputExcerpt = prompt.Length > 120 ? prompt[..120] + "…" : prompt,
                Outcome = "Blocked"
            });
        }
    }
}
