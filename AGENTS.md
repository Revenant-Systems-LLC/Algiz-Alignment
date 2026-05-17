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

### Bug-Fix Pass (2026-05-17) — COMPLETED

A full code review was done across every .cs file. All 16 fixes have been applied and verified.

**COMPLETED fixes:**
1. CRITICAL: Sync-over-async deadlock in `SageGuardrailController.Evaluate()` — removed the `.GetAwaiter().GetResult()` wrapper, replaced sync `Evaluate()` with async `EvaluateAsync()` convenience overload, updated all 3 callers (SageAgent, GovernedEngine, ProxyPipeline) to `await EvaluateAsync()`.
2. CRITICAL: Gemini API key exposed in URL query string — `GeminiLLMProvider.BuildEndpoint()` now uses `x-goog-api-key` header instead of `?key=`. `GeminiLiveAudioProvider` WebSocket uses `ws.Options.SetRequestHeader()`.
3. CRITICAL: HttpClient lifecycle — All providers (`AnthropicProvider`, `OpenAIProvider`, `GeminiLLMProvider`, `OllamaProvider`, `OpenAICompatibleProvider`) now implement `IDisposable` with `_ownsHttpClient` flag. Only self-created clients are disposed.
4. HIGH: Race condition in `GovernedEngine` — `_statsLock` is now used around all metric writes/reads for `_status`, `_locked`, `_lockReason`, `_statusDetail`, and `_lastActivityAt`.
5. HIGH: Empty `Dispose()` in `SageRuntime` — now disposes `_llm` if it implements `IDisposable`.
6. HIGH: `SecretLoader.LoadConfig()` bare catch — now catches `JsonException` specifically, lets other exceptions propagate.
7. HIGH: `SageMetrics.CalculatePerplexity()` — returns `float.MaxValue` for empty text instead of `0f`.
8. HIGH: `ProxyPipeline.ProcessResponse()` — `_engine` is now cached/injected in constructor instead of created per request.
9. MEDIUM: API endpoints (`/api/profiles/{id}/start`, `/stop`, `/reset`) — now catch `KeyNotFoundException` and return 404 instead of 500.
10. MEDIUM: `SageRuntime.ExecuteChi` — changed `1.0f / best.perplexity` to `1.0f / (1.0f + best.perplexity)` to avoid Infinity.
11. MEDIUM: `GeminiLiveSession.ReceiveAsync` — now catches `JsonException` and `FormatException` specifically instead of bare catch.
12. MEDIUM: `OpenAICompatibleProvider.GenerateAsync` — now calls `EnsureSuccessStatusCode()` before parsing JSON.
13. MEDIUM: `ILLMProvider.GetEmbeddingAsync` and `GetAttentionWeightsAsync` — now have `CancellationToken` parameter with default value. Updated interface and all implementations (including test mocks).
14. MEDIUM: `SimpleWhitespaceTokenizer.Encode` — replaced `string.GetHashCode()` with deterministic FNV-1a hash.
15. MEDIUM: `_statsLock` in `GovernedEngine` — covered by fix #4.
16. MEDIUM: `BuildUserPayload` — extracted to shared static helper `PromptHelpers.BuildUserPayload()`. Removed duplicate implementations from `AnthropicProvider`, `OpenAIProvider`, `GeminiLLMProvider`.

**Verification:** All 67 tests pass (26 Core + 25 Proxy + 16 Governance). Build succeeds with no warnings.

### Bug-Fix Pass 2 (2026-05-17) — COMPLETED

Second full code review across all .cs files. One new fix applied.

**COMPLETED fixes:**
17. CRITICAL: `ProviderFactoryHelper.Create()` throws NotImplementedException — implemented concrete provider resolution mapping `profile.ProviderType` to `ILLMProvider` implementations (Gemini, Anthropic, OpenAI, Ollama, OpenAI-Compatible). Resolves API profile creation failure.

**Verification:** All 67 tests pass. Code review returns clean — no additional issues found.

### Bug-Fix Pass 3 (2026-05-17) — COMPLETED

Third full code review with AI system skills loaded (csharp-reviewer, code-reviewer, silent-failure-hunter, systematic-debugging). Three new fixes applied.

**COMPLETED fixes:**
18. MEDIUM: Silent failure in AudioCapture channel write — `AudioCapture.cs:55` ignored `TryWrite()` return value. Added comment explaining DropOldest behavior and noting future logging consideration.
19. MEDIUM: Silent failure in AudioPlayback buffer — `AudioPlayback.cs:34` `AddSamples()` could throw. Added try-catch with `ObjectDisposedException` handling and debug logging for other errors.
20. LOW: Empty catch blocks in QC checks lack logging — `Checks.cs` empty `catch (JsonException)` blocks and result generation without diagnostics. Added `Debug.WriteLine` logging for malformed chunks and check findings.

**Verification:** All 67 tests pass. Code review returns clean — no additional issues found.

### What's Next
- **Continue bug-fix pass** — apply fixes 3-16 above, then re-scan until clean
- Cascade resumes UI work binding to the real Governance types
- Wire ProfileManager into Console app and Proxy startup
- Profile persistence (save/load profiles to disk)
- Multi-upstream proxy routing (different profiles -> different upstream APIs)
- Evaluation benchmarks (per whitepaper Section 18)
