# SAIGE-RAGE Project Rules

## Commit Attribution

**NEVER include AI attribution in commits.** No "Generated with", no "Co-Authored-By" lines
referencing any AI tool (Devin, Claude, Copilot, Cascade, or anything else). This applies
to all commits, all branches, all messages. No exceptions.

This is a hard requirement. SAIGE-RAGE is an AI governance system. Its credibility depends
on being recognized as human-designed and human-authored. AI tools are implementation aids,
not authors.

## Build & Test

- Solution: `dotnet build SAIGE-RAGE.sln`
- Tests: `dotnet test SAIGE-RAGE.sln` (67 tests: 26 Core + 25 Proxy + 16 Governance)
- All tests must pass before committing.

## Secrets

- API keys are loaded from `B:\secrets\` via `SecretLoader`, never from system environment variables.
- Never log, print, or commit real API keys.

## Architecture

- Core types take precedence. The UI binds to Core, never the reverse.
- Multi-profile governance lives in `src/SageRage.Core/Governance/`.
- Whitepaper is at `docs/SAIGE-RAGE-Whitepaper.docx` (Draft 4). Use `python-docx` via `uv` to extract.

## Product Direction

SAIGE-RAGE is an **enterprise AI safety layer**. The product sits in front of all of a
company's AI agents and governs them simultaneously. An ops person looks at one dashboard
and knows which of their 50 agents is drifting, flagged, or clean. This is infrastructure,
not a developer toy.

**Multi-profile model:** Each GovernanceProfile = one governed AI system (its own LLM config,
operator pipeline, ethical stack, QC thresholds, avatar, persona). Multiple profiles run
simultaneously. The ProfileManager owns all of them.

**UI paradigm:** Safety operations center. Dashboard shows all active profiles as tiles with
live state. Each tile has a VAM radar chart (Valence, Activation, Malice + Coherence, Entropy,
Drift, QC score). Avatars are per-profile visual identifiers for fast scanning. The visual
style is "gamey" (radar charts, color-coded status, health-bar-style gauges) because it makes
dense operational data instantly parsable -- not for engagement/retention.

**Cascade (Windsurf AI) is building the WPF UI** (`src/SageRage.UI/`). She has been told to
bind only to Core types. The UI adapts to Core, never the reverse. She was paused while
the governance foundation was built and should now be resumed.

## Current State (as of 2026-05-17)

### What's Done
- Operator algebra runtime (Containment, Omega, Chi, Sigma) -- `RageEngine.cs`
- Ethics enforcement (Layer 0-3) -- `RageEthicsStack` in `SageTypes.cs`
- Quality control / guardrails -- `QualityControl/` directory
- Emotional modeling (VAD + VAM/Malice derivation) -- `SageEmotionalTracker.cs`, `GovernedEngine.cs`
- 6 LLM providers (Gemini, OpenAI, Claude, Ollama, OpenAI-Compatible, Gemini Live Audio)
- Alignment proxy -- `src/SageRage.Proxy/` (Kestrel HTTP, streaming, trace headers)
- Secure key management -- `SecretLoader.cs` + PowerShell `Load-Secrets`/`Unload-Secrets`
- Multi-profile governance foundation -- `src/SageRage.Core/Governance/`
  - `GovernanceProfile` (per-system config)
  - `ProfileSnapshot` (UI data surface: VAD, VAM, metrics, stats, status)
  - `GovernedEngine` (per-profile engine wrapper with Malice derivation)
  - `ProfileManager` (thread-safe lifecycle, routing, snapshots, events)
- README enriched with full whitepaper content
- 67 tests passing (26 Core + 25 Proxy + 16 Governance)
- System environment variables with API keys removed (user-level all cleared,
  machine-level BRAVE_API_KEY and NEWS_API_KEY intentionally left as free-tier)

### What's Next
- Cascade resumes UI work binding to the real Governance types
- Wire ProfileManager into Console app and Proxy startup
- Profile persistence (save/load profiles to disk)
- Multi-upstream proxy routing (different profiles -> different upstream APIs)
- Evaluation benchmarks (per whitepaper Section 18)
