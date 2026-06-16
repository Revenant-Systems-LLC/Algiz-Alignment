# AGENTS.md

## Cursor Cloud specific instructions

### What runs on Linux (and what does not)

This is a .NET 8 solution plus a Next.js web frontend and a small Python harness.
Not everything is Linux-runnable:

- **`src/SageRage.Api`** (ASP.NET minimal API) + **`src/SageRage.Web`** (Next.js dashboard)
  are the end-to-end product that runs on Linux **without any LLM API keys**. The Web
  dashboard fetches `http://localhost:5000/api/dashboard` directly, so the API must listen
  on port **5000**. Start it with:
  `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://0.0.0.0:5000 dotnet run --project src/SageRage.Api`
  (Development enables Swagger UI at `/swagger`.) Run the web with `npm --prefix src/SageRage.Web run dev` (port 3000).
- **`src/SageRage.UI`** is WPF (`net8.0-windows`) and **cannot build on Linux** (NETSDK1100).
- **`src/SageRage.Console`**, **`src/SageRage.Cli`**, **`src/SageRage.Proxy`** build on Linux but
  only do useful work with real LLM provider keys (see `.env.example`). The Console secret
  wizard defaults to a Windows path (`B:\secrets`) and is interactive.

### Do NOT build/restore the solution file on Linux

`dotnet build SAIGE-RAGE.sln` / `dotnet restore SAIGE-RAGE.sln` both fail on Linux for two
reasons: (1) the `.sln` references `tests/SageRage.Core.Tests` with the wrong case while the
real folder is `Tests/` (case-sensitive FS), and (2) it includes the Windows-only WPF UI.
Build and test the projects individually instead, e.g.:

- `dotnet build src/SageRage.Api` (also builds `SageRage.Core`)
- `dotnet build src/SageRage.Console` / `src/SageRage.Cli` / `src/SageRage.Proxy`
- `dotnet test Tests/SageRage.Core.Tests`

### Tests

- `Tests/SageRage.Core.Tests` compiles and passes (67 tests) — this is the canonical core
  suite and exercises the governance engine, ethics gate, QC, and operator pipeline with mocks.
- `Tests/SageRage.Proxy.Tests` currently **does not compile** — a pre-existing source/test
  mismatch (`ProxyPipelineTests` calls `new ProxyPipeline(config)` / `ProcessResponse(..., provider)`
  but the source requires `ProxyPipeline(config, ILLMProvider)` / `ProcessResponse(string, string, CancellationToken)`).
  This is an application bug, not an environment problem.

### Other known pre-existing bug

- `src/SageRage.Web/app/page.tsx` has a duplicate `default` export (a correct `dynamic()`
  wrapper followed by a leftover copy of `DashboardPage.tsx`), which breaks `next build` and
  `next dev` on the `/` route. The backend API is unaffected.
