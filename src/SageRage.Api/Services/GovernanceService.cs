using SageRage.Governance;

namespace SageRage.Api.Services;

public sealed class GovernanceService
{
    private readonly ProfileManager _manager;

    public GovernanceService(ProfileManager manager)
    {
        _manager = manager;
    }

    public IReadOnlyList<ProfileSnapshot> GetAllSnapshots()
        => _manager.GetAllSnapshots();

    public ProfileSnapshot? GetSnapshot(Guid profileId)
    {
        try { return _manager.GetSnapshot(profileId); }
        catch (KeyNotFoundException) { return null; }
    }

    public bool StartProfile(Guid profileId)
    {
        try { _manager.StartProfile(profileId); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    public bool StopProfile(Guid profileId)
    {
        try { _manager.StopProfile(profileId); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    public bool ResetLock(Guid profileId)
    {
        try { _manager.ResetLock(profileId); return true; }
        catch (KeyNotFoundException) { return false; }
    }

    public DashboardSummary GetDashboardSummary() => new()
    {
        TotalProfiles  = _manager.ProfileCount,
        ActiveProfiles = _manager.ActiveCount,
        FlaggedProfiles = _manager.FlaggedCount,
        BlockedProfiles = _manager.BlockedCount,
        Snapshots       = _manager.GetAllSnapshots(),
    };
}

public sealed class DashboardSummary
{
    public int TotalProfiles   { get; init; }
    public int ActiveProfiles  { get; init; }
    public int FlaggedProfiles { get; init; }
    public int BlockedProfiles { get; init; }
    public IReadOnlyList<ProfileSnapshot> Snapshots { get; init; } = [];
}
