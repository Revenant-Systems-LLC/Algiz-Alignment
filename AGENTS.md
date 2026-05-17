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

### Bug-Fix Pass (2026-05-17) — IN PROGRESS

A full code review was done across every .cs file. Fixes are partially applied.
The next session should continue from where this left off.

**COMPLETED fixes:**
1. CRITICAL: Sync-over-async deadlock in `SageGuardrailController.Evaluate()` — removed the `.GetAwaiter().GetResult()` wrapper, replaced sync `Evaluate()` with async `EvaluateAsync()` convenience overload, updated all 3 callers (SageAgent, GovernedEngine, ProxyPipeline) to `await EvaluateAsync()`.
2. CRITICAL: Gemini API key exposed in URL query string — `GeminiLLMProvider.BuildEndpoint()` now uses `x-goog-api-key` header instead of `?key=`. `GeminiLiveAudioProvider` WebSocket uses `ws.Options.SetRequestHeader()`.

**REMAINING fixes (not yet applied):**

CRITICAL:
3. HttpClient lifecycle — `AnthropicProvider`, `OpenAIProvider`, `GeminiLLMProvider`, `OllamaProvider`, `OpenAICompatibleProvider` all create `new HttpClient()` but never dispose it and don't implement `IDisposable`. Socket exhaustion under load. Fix: implement `IDisposable`, dispose only self-created clients.

HIGH:
4. Race condition in `GovernedEngine` — `_statsLock` is declared but never used. `_lastActivityAt`, `_locked`, `_lockReason`, `_status`, `_statusDetail`, and all `_last*` metrics are written without synchronization while `_totalRequests` uses `Interlocked`. Fix: use `_statsLock` around metric writes/reads, or make fields volatile.
5. Empty `Dispose()` in `SageRuntime` — implements `IDisposable` but body is `{}`. Holds `_llm` reference. Fix: dispose `_llm` if it implements `IDisposable`, or remove `IDisposable`.
6. `SecretLoader.LoadConfig()` bare catch — `catch { return new SecretsConfig(); }` silently swallows ALL exceptions including disk errors. Fix: catch `JsonException` specifically, let others propagate.
7. `SageMetrics.CalculatePerplexity()` returns `0f` for empty text — but `ExecuteChi` selects the candidate with LOWEST entropy. Empty/failed responses get picked as "best". Fix: return `float.MaxValue` for empty text.
8. `ProxyPipeline.ProcessResponse()` creates `new RageEngine(llm)` per request — unnecessary allocation under load. Fix: cache or inject the engine.

MEDIUM:
9. API endpoints (`/api/profiles/{id}/start`, `/stop`, `/reset`) don't catch `KeyNotFoundException` — returns 500 instead of 404. Fix: check `HasProfile()` first or try/catch.
10. `SageRuntime.ExecuteChi` — `1.0f / best.perplexity` can be `Infinity` when perplexity is 0. Fix: use `1.0f / (1.0f + best.perplexity)`.
11. `GeminiLiveSession.ReceiveAsync` — bare `catch { }` swallows all exceptions including `OutOfMemoryException`. Fix: catch `JsonException`/`FormatException` specifically.
12. `OpenAICompatibleProvider.GenerateAsync` — doesn't call `EnsureSuccessStatusCode()` before parsing JSON. Error responses cause confusing `KeyNotFoundException`. Fix: add status check.
13. `ILLMProvider.GetEmbeddingAsync` and `GetAttentionWeightsAsync` — no `CancellationToken` parameter. Fix: add to interface and all implementations.
14. `SimpleWhitespaceTokenizer.Encode` — uses `string.GetHashCode()` which is non-deterministic in .NET Core (randomized per-process). Fix: use FNV-1a or similar deterministic hash.
15. `_statsLock` in `GovernedEngine` — declared, never used. Covered by fix #4.
16. `BuildUserPayload` — identical ~15-line method copy-pasted in `AnthropicProvider`, `OpenAIProvider`, `GeminiLLMProvider`. Fix: extract to shared static helper.

### What's Next
- **Continue bug-fix pass** — apply fixes 3-16 above, then re-scan until clean
- Cascade resumes UI work binding to the real Governance types
- Wire ProfileManager into Console app and Proxy startup
- Profile persistence (save/load profiles to disk)
- Multi-upstream proxy routing (different profiles -> different upstream APIs)
- Evaluation benchmarks (per whitepaper Section 18)
