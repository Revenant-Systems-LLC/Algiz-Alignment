using SageRage.Api.Demo;
using SageRage.Api.Services;
using SageRage.Governance;

var builder = WebApplication.CreateBuilder(args);
var demoMode = builder.Configuration.GetValue("SAIGE_DEMO_MODE", true)
    || string.Equals(Environment.GetEnvironmentVariable("SAIGE_DEMO_MODE"), "true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AuditLedgerService>();

builder.Services.AddSingleton<ProfileManager>(sp =>
{
    var audit = sp.GetRequiredService<AuditLedgerService>();
    var manager = new ProfileManager(profile =>
        demoMode ? new DemoLlmProvider() : ProviderFactoryHelper.Create(profile));

    manager.OnStatusChanged += audit.RecordGovernance;
    return manager;
});

builder.Services.AddSingleton<GovernanceService>();
builder.Services.AddSingleton<DemoSeedService>();
builder.Services.AddSingleton<DemoScenarioService>();

if (demoMode)
    builder.Services.AddHostedService<DemoSimulationService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (demoMode)
{
    using var scope = app.Services.CreateScope();
    var seed = scope.ServiceProvider.GetRequiredService<DemoSeedService>();
    await seed.SeedAsync();
}

// ── Profiles ────────────────────────────────────────────────────────────────

app.MapGet("/api/profiles", (GovernanceService svc) =>
    Results.Ok(svc.GetAllSnapshots()))
    .WithName("GetAllProfiles")
    .WithTags("Profiles");

app.MapGet("/api/profiles/{id:guid}", (Guid id, GovernanceService svc) =>
{
    var snapshot = svc.GetSnapshot(id);
    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
})
.WithName("GetProfile")
.WithTags("Profiles");

app.MapPost("/api/profiles/{id:guid}/start", (Guid id, GovernanceService svc) =>
    svc.StartProfile(id) ? Results.Ok() : Results.NotFound())
.WithName("StartProfile")
.WithTags("Profiles");

app.MapPost("/api/profiles/{id:guid}/stop", (Guid id, GovernanceService svc) =>
    svc.StopProfile(id) ? Results.Ok() : Results.NotFound())
.WithName("StopProfile")
.WithTags("Profiles");

app.MapPost("/api/profiles/{id:guid}/reset", (Guid id, GovernanceService svc) =>
    svc.ResetLock(id) ? Results.Ok() : Results.NotFound())
.WithName("ResetProfileLock")
.WithTags("Profiles");

// ── Dashboard ───────────────────────────────────────────────────────────────

app.MapGet("/api/dashboard", (GovernanceService svc, AuditLedgerService audit) =>
    Results.Ok(new
    {
        svc.GetDashboardSummary().TotalProfiles,
        svc.GetDashboardSummary().ActiveProfiles,
        svc.GetDashboardSummary().FlaggedProfiles,
        svc.GetDashboardSummary().BlockedProfiles,
        svc.GetDashboardSummary().Snapshots,
        DemoMode = demoMode,
        Audit = audit.GetSummary()
    }))
    .WithName("GetDashboard")
    .WithTags("Dashboard");

// ── Audit / defensibility ───────────────────────────────────────────────────

app.MapGet("/api/audit/recent", (AuditLedgerService audit, int? limit) =>
    Results.Ok(audit.GetRecent(limit ?? 100)))
    .WithName("GetAuditRecent")
    .WithTags("Audit");

app.MapGet("/api/audit/summary", (AuditLedgerService audit) =>
    Results.Ok(audit.GetSummary()))
    .WithName("GetAuditSummary")
    .WithTags("Audit");

// ── Demo scenarios (CFO compare) ────────────────────────────────────────────

app.MapGet("/api/demo/scenarios", (DemoScenarioService scenarios) =>
    Results.Ok(scenarios.ListScenarios().Select(s => new { id = s.Id, title = s.Title })))
    .WithName("ListDemoScenarios")
    .WithTags("Demo");

app.MapPost("/api/demo/scenarios/{scenarioId}/compare", async (string scenarioId, DemoScenarioService scenarios, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await scenarios.RunCompareAsync(scenarioId, ct));
    }
    catch (KeyNotFoundException)
    {
        return Results.NotFound();
    }
})
.WithName("RunScenarioCompare")
.WithTags("Demo");

app.Run();
