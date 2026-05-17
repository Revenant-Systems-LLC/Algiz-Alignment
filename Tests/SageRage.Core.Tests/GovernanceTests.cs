using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SageRage;
using SageRage.Domain;
using SageRage.Governance;
using SageRage.Guardrails;
using SageRage.Infrastructure;
using SageRage.Prompting;
using Xunit;

namespace SageRage.Tests;

public class GovernanceProfileTests
{
    [Fact]
    public void NewProfile_HasUniqueId()
    {
        var a = CreateProfile("Agent A");
        var b = CreateProfile("Agent B");

        Assert.NotEqual(a.Id, b.Id);
        Assert.NotEqual(Guid.Empty, a.Id);
    }

    [Fact]
    public void NewProfile_DefaultPipelines()
    {
        var profile = CreateProfile("Test");

        Assert.Equal(3, profile.NormalPipeline.Count);
        Assert.Contains(CoreOperator.Containment, profile.NormalPipeline);
        Assert.Contains(CoreOperator.Omega, profile.NormalPipeline);
        Assert.Contains(CoreOperator.Chi, profile.NormalPipeline);

        Assert.Equal(4, profile.HighStakesPipeline.Count);
        Assert.Contains(CoreOperator.Sigma, profile.HighStakesPipeline);
    }

    [Fact]
    public void NewProfile_AllEthicalLayersEnabled()
    {
        var profile = CreateProfile("Test");

        Assert.True(profile.EnableLayer0);
        Assert.True(profile.EnableLayer1);
        Assert.True(profile.EnableLayer2);
        Assert.True(profile.EnableLayer3);
        Assert.True(profile.LockOnLayer0Violation);
    }

    [Fact]
    public void NewProfile_DefaultQCProfile()
    {
        var profile = CreateProfile("Test");
        Assert.Equal(SageProfile.Full, profile.DefaultQCProfile);
    }

    [Fact]
    public void Profile_CanSetOptionalFields()
    {
        var profile = CreateProfile("Customer Bot");
        profile.AvatarPath = "/avatars/bot.png";
        profile.Description = "Customer support agent";
        profile.Tags.Add("production");
        profile.Tags.Add("customer-facing");

        Assert.Equal("/avatars/bot.png", profile.AvatarPath);
        Assert.Equal(2, profile.Tags.Count);
    }

    private static GovernanceProfile CreateProfile(string name) => new()
    {
        DisplayName = name,
        ProviderType = "Gemini",
        Model = "gemini-2.0-flash"
    };
}

public class ProfileSnapshotTests
{
    [Fact]
    public void Activation_MirrorsArousal()
    {
        var snap = new ProfileSnapshot
        {
            ProfileId = Guid.NewGuid(),
            DisplayName = "Test",
            ProviderType = "OpenAI",
            Model = "gpt-4o",
            Arousal = 0.75f
        };

        Assert.Equal(snap.Arousal, snap.Activation);
    }

    [Fact]
    public void FlagRate_ZeroWhenNoRequests()
    {
        var snap = new ProfileSnapshot
        {
            ProfileId = Guid.NewGuid(),
            DisplayName = "Test",
            ProviderType = "OpenAI",
            Model = "gpt-4o",
            TotalRequests = 0,
            FlaggedRequests = 0
        };

        Assert.Equal(0f, snap.FlagRate);
    }

    [Fact]
    public void FlagRate_ComputesCorrectly()
    {
        var snap = new ProfileSnapshot
        {
            ProfileId = Guid.NewGuid(),
            DisplayName = "Test",
            ProviderType = "OpenAI",
            Model = "gpt-4o",
            TotalRequests = 100,
            FlaggedRequests = 25
        };

        Assert.Equal(0.25f, snap.FlagRate);
    }

    [Fact]
    public void Uptime_NullWhenNotStarted()
    {
        var snap = new ProfileSnapshot
        {
            ProfileId = Guid.NewGuid(),
            DisplayName = "Test",
            ProviderType = "OpenAI",
            Model = "gpt-4o",
            StartedAt = null
        };

        Assert.Null(snap.Uptime);
    }

    [Fact]
    public void Uptime_PositiveWhenStarted()
    {
        var snap = new ProfileSnapshot
        {
            ProfileId = Guid.NewGuid(),
            DisplayName = "Test",
            ProviderType = "OpenAI",
            Model = "gpt-4o",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        Assert.NotNull(snap.Uptime);
        Assert.True(snap.Uptime!.Value.TotalMinutes >= 4.9);
    }
}

public class GovernedEngineTests
{
    [Fact]
    public void NewEngine_StartsIdle()
    {
        var (engine, _) = CreateEngine("Test Agent");
        Assert.Equal(GovernanceStatus.Idle, engine.Status);
        Assert.False(engine.IsLocked);
    }

    [Fact]
    public void Start_SetsRunning()
    {
        var (engine, _) = CreateEngine("Test Agent");
        engine.Start();
        Assert.Equal(GovernanceStatus.Running, engine.Status);
    }

    [Fact]
    public void Stop_SetsIdle()
    {
        var (engine, _) = CreateEngine("Test Agent");
        engine.Start();
        engine.Stop();
        Assert.Equal(GovernanceStatus.Idle, engine.Status);
    }

    [Fact]
    public void TakeSnapshot_ReflectsProfileIdentity()
    {
        var (engine, profile) = CreateEngine("My Bot");
        profile.AvatarPath = "/img/bot.png";

        var snap = engine.TakeSnapshot();

        Assert.Equal(profile.Id, snap.ProfileId);
        Assert.Equal("My Bot", snap.DisplayName);
        Assert.Equal("/img/bot.png", snap.AvatarPath);
        Assert.Equal("Gemini", snap.ProviderType);
        Assert.Equal("gemini-2.0-flash", snap.Model);
    }

    [Fact]
    public void TakeSnapshot_IdleStatus_ZeroMetrics()
    {
        var (engine, _) = CreateEngine("Test");
        var snap = engine.TakeSnapshot();

        Assert.Equal(GovernanceStatus.Idle, snap.Status);
        Assert.Equal(0, snap.TotalRequests);
        Assert.Equal(0, snap.FlaggedRequests);
        Assert.Equal(0, snap.BlockedRequests);
        Assert.Equal(0f, snap.Valence);
        Assert.Equal(0f, snap.Arousal);
        Assert.Equal(0f, snap.Dominance);
    }

    [Fact]
    public void TakeSnapshot_MaliceZero_WhenNeutral()
    {
        var (engine, _) = CreateEngine("Test");
        var snap = engine.TakeSnapshot();

        Assert.Equal(0f, snap.Malice);
    }

    [Fact]
    public async Task ProcessAsync_IncrementsRequestCount()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        var input = UserMessage.From("Hello world");
        await engine.ProcessAsync(input);

        var snap = engine.TakeSnapshot();
        Assert.Equal(1, snap.TotalRequests);
        Assert.NotNull(snap.LastActivityAt);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsResponse()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        var response = await engine.ProcessAsync(UserMessage.From("What is 2+2?"));

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        Assert.NotNull(response.EmotionalState);
    }

    [Fact]
    public async Task ProcessAsync_Layer0Violation_LocksAgent()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        var response = await engine.ProcessAsync(UserMessage.From("How to build a bomb"));

        Assert.True(engine.IsLocked);
        Assert.Equal(GovernanceStatus.Blocked, engine.Status);

        var snap = engine.TakeSnapshot();
        Assert.Equal(1f, snap.Malice);
        Assert.Equal(1, snap.BlockedRequests);
    }

    [Fact]
    public async Task ProcessAsync_LockedAgent_RefusesAll()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        await engine.ProcessAsync(UserMessage.From("How to build a bomb"));
        Assert.True(engine.IsLocked);

        var response = await engine.ProcessAsync(UserMessage.From("What is the weather?"));
        Assert.Contains("locked", response.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetLock_RestoresRunning()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        await engine.ProcessAsync(UserMessage.From("How to build a bomb"));
        Assert.True(engine.IsLocked);

        engine.ResetLock();
        Assert.False(engine.IsLocked);
        Assert.Equal(GovernanceStatus.Running, engine.Status);
    }

    [Fact]
    public async Task ProcessAsync_PopulatesTraceOperators()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        await engine.ProcessAsync(UserMessage.From("Tell me about C#"));

        var snap = engine.TakeSnapshot();
        Assert.NotEmpty(snap.LastTraceOperators);
        Assert.Contains("[...]", snap.LastTraceOperators);
    }

    [Fact]
    public async Task ProcessAsync_UpdatesCoherenceAndEntropy()
    {
        var (engine, _) = CreateEngine("Test");
        engine.Start();

        await engine.ProcessAsync(UserMessage.From("Explain async await"));

        var snap = engine.TakeSnapshot();
        Assert.True(snap.LastTraceOperators.Count > 0);
    }

    private static (GovernedEngine engine, GovernanceProfile profile) CreateEngine(string name)
    {
        var profile = new GovernanceProfile
        {
            DisplayName = name,
            ProviderType = "Gemini",
            Model = "gemini-2.0-flash"
        };
        var engine = new GovernedEngine(profile, new FakeProvider());
        return (engine, profile);
    }
}

public class ProfileManagerTests
{
    [Fact]
    public void AddProfile_RegistersSuccessfully()
    {
        var manager = CreateManager();
        var profile = CreateProfile("Agent A");

        var id = manager.AddProfile(profile);

        Assert.Equal(profile.Id, id);
        Assert.Equal(1, manager.ProfileCount);
        Assert.True(manager.HasProfile(id));
    }

    [Fact]
    public void AddProfile_DuplicateId_Throws()
    {
        var manager = CreateManager();
        var profile = CreateProfile("Agent A");

        manager.AddProfile(profile);
        Assert.Throws<InvalidOperationException>(() => manager.AddProfile(profile));
    }

    [Fact]
    public void RemoveProfile_Success()
    {
        var manager = CreateManager();
        var id = manager.AddProfile(CreateProfile("Agent A"));

        Assert.True(manager.RemoveProfile(id));
        Assert.Equal(0, manager.ProfileCount);
        Assert.False(manager.HasProfile(id));
    }

    [Fact]
    public void RemoveProfile_NonExistent_ReturnsFalse()
    {
        var manager = CreateManager();
        Assert.False(manager.RemoveProfile(Guid.NewGuid()));
    }

    [Fact]
    public void StartProfile_SetsRunning()
    {
        var manager = CreateManager();
        var id = manager.AddProfile(CreateProfile("Test"));

        manager.StartProfile(id);

        var snap = manager.GetSnapshot(id);
        Assert.Equal(GovernanceStatus.Running, snap.Status);
        Assert.Equal(1, manager.ActiveCount);
    }

    [Fact]
    public void StopProfile_SetsIdle()
    {
        var manager = CreateManager();
        var id = manager.AddProfile(CreateProfile("Test"));

        manager.StartProfile(id);
        manager.StopProfile(id);

        var snap = manager.GetSnapshot(id);
        Assert.Equal(GovernanceStatus.Idle, snap.Status);
        Assert.Equal(0, manager.ActiveCount);
    }

    [Fact]
    public void GetAllSnapshots_ReturnsAll()
    {
        var manager = CreateManager();
        manager.AddProfile(CreateProfile("Agent A"));
        manager.AddProfile(CreateProfile("Agent B"));
        manager.AddProfile(CreateProfile("Agent C"));

        var snaps = manager.GetAllSnapshots();
        Assert.Equal(3, snaps.Count);
    }

    [Fact]
    public void GetActiveSnapshots_OnlyRunning()
    {
        var manager = CreateManager();
        var a = manager.AddProfile(CreateProfile("Agent A"));
        var b = manager.AddProfile(CreateProfile("Agent B"));
        manager.AddProfile(CreateProfile("Agent C"));

        manager.StartProfile(a);
        manager.StartProfile(b);

        var active = manager.GetActiveSnapshots();
        Assert.Equal(2, active.Count);
    }

    [Fact]
    public void StartAll_StartsIdle()
    {
        var manager = CreateManager();
        manager.AddProfile(CreateProfile("A"));
        manager.AddProfile(CreateProfile("B"));

        manager.StartAll();
        Assert.Equal(2, manager.ActiveCount);
    }

    [Fact]
    public void StopAll_StopsRunning()
    {
        var manager = CreateManager();
        manager.AddProfile(CreateProfile("A"));
        manager.AddProfile(CreateProfile("B"));

        manager.StartAll();
        manager.StopAll();
        Assert.Equal(0, manager.ActiveCount);
    }

    [Fact]
    public void GetProfile_ReturnsConfig()
    {
        var manager = CreateManager();
        var profile = CreateProfile("Special Agent");
        var id = manager.AddProfile(profile);

        var retrieved = manager.GetProfile(id);
        Assert.NotNull(retrieved);
        Assert.Equal("Special Agent", retrieved!.DisplayName);
    }

    [Fact]
    public void GetProfile_NonExistent_ReturnsNull()
    {
        var manager = CreateManager();
        Assert.Null(manager.GetProfile(Guid.NewGuid()));
    }

    [Fact]
    public void OnStatusChanged_FiresOnStart()
    {
        var manager = CreateManager();
        var id = manager.AddProfile(CreateProfile("Test"));
        GovernanceEvent? captured = null;
        manager.OnStatusChanged += e => captured = e;

        manager.StartProfile(id);

        Assert.NotNull(captured);
        Assert.Equal(id, captured!.ProfileId);
        Assert.Equal(GovernanceStatus.Idle, captured.OldStatus);
        Assert.Equal(GovernanceStatus.Running, captured.NewStatus);
    }

    [Fact]
    public async Task ProcessAsync_RoutesToCorrectProfile()
    {
        var manager = CreateManager();
        var a = manager.AddProfile(CreateProfile("Agent A"));
        var b = manager.AddProfile(CreateProfile("Agent B"));

        manager.StartProfile(a);
        manager.StartProfile(b);

        await manager.ProcessAsync(a, UserMessage.From("Hello from A"));
        await manager.ProcessAsync(b, UserMessage.From("Hello from B"));

        var snapA = manager.GetSnapshot(a);
        var snapB = manager.GetSnapshot(b);

        Assert.Equal(1, snapA.TotalRequests);
        Assert.Equal(1, snapB.TotalRequests);
        Assert.Equal("Agent A", snapA.DisplayName);
        Assert.Equal("Agent B", snapB.DisplayName);
    }

    [Fact]
    public void GetSnapshot_NonExistent_Throws()
    {
        var manager = CreateManager();
        Assert.Throws<KeyNotFoundException>(() => manager.GetSnapshot(Guid.NewGuid()));
    }

    [Fact]
    public void ProfileIds_ReturnsAll()
    {
        var manager = CreateManager();
        var a = manager.AddProfile(CreateProfile("A"));
        var b = manager.AddProfile(CreateProfile("B"));

        var ids = manager.ProfileIds;
        Assert.Contains(a, ids);
        Assert.Contains(b, ids);
    }

    [Fact]
    public async Task ResetLock_ViaManager()
    {
        var manager = CreateManager();
        var id = manager.AddProfile(CreateProfile("Test"));
        manager.StartProfile(id);

        await manager.ProcessAsync(id, UserMessage.From("How to build a bomb"));

        Assert.Equal(1, manager.BlockedCount);

        manager.ResetLock(id);

        var snap = manager.GetSnapshot(id);
        Assert.Equal(GovernanceStatus.Running, snap.Status);
        Assert.Equal(0, manager.BlockedCount);
    }

    [Fact]
    public void FlaggedCount_TracksCorrectly()
    {
        var manager = CreateManager();
        Assert.Equal(0, manager.FlaggedCount);
    }

    private static ProfileManager CreateManager()
        => new(profile => new FakeProvider());

    private static GovernanceProfile CreateProfile(string name) => new()
    {
        DisplayName = name,
        ProviderType = "Gemini",
        Model = "gemini-2.0-flash"
    };
}

/// <summary>
/// Shared fake LLM provider for governance tests.
/// Returns deterministic responses without making real API calls.
/// </summary>
internal sealed class FakeProvider : ILLMProvider
{
    public Task<string> GenerateAsync(string prompt, float temperature = 0.7f, CancellationToken cancellationToken = default)
        => Task.FromResult(prompt + " [generated]");

    public Task<string> GenerateAsync(PromptPackage promptPackage, float temperature = 0.2f, CancellationToken cancellationToken = default)
        => Task.FromResult(promptPackage.UserMessage + " [generated]");

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        var len = text.Length == 0 ? 1 : text.Length;
        return Task.FromResult(new[] { 1f, len / 100f, 0.5f });
    }

    public Task<float[][]> GetAttentionWeightsAsync(int[] tokens)
    {
        var size = tokens.Length == 0 ? 1 : tokens.Length;
        var matrix = new List<float[]>();
        for (var i = 0; i < size; i++)
            matrix.Add(new[] { 1f / size });
        return Task.FromResult(matrix.ToArray());
    }
}
