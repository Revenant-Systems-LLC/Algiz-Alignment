using SageRage.Api.Services;
using SageRage.Governance;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ProfileManager>(sp =>
    new ProfileManager(profile => ProviderFactoryHelper.Create(profile)));
builder.Services.AddSingleton<GovernanceService>();

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
{
    return svc.StartProfile(id) ? Results.Ok() : Results.NotFound();
})
.WithName("StartProfile")
.WithTags("Profiles");

app.MapPost("/api/profiles/{id:guid}/stop", (Guid id, GovernanceService svc) =>
{
    return svc.StopProfile(id) ? Results.Ok() : Results.NotFound();
})
.WithName("StopProfile")
.WithTags("Profiles");

app.MapPost("/api/profiles/{id:guid}/reset", (Guid id, GovernanceService svc) =>
{
    return svc.ResetLock(id) ? Results.Ok() : Results.NotFound();
})
.WithName("ResetProfileLock")
.WithTags("Profiles");

// ── Dashboard Summary ────────────────────────────────────────────────────────

app.MapGet("/api/dashboard", (GovernanceService svc) =>
    Results.Ok(svc.GetDashboardSummary()))
    .WithName("GetDashboard")
    .WithTags("Dashboard");

app.Run();
