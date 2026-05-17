# SAGE-RAGE

**Symbolic Alignment and Generative Ethics / Recursive Alignment and Generative Ethics**

A runtime alignment engine for LLM applications. SAGE-RAGE intercepts, evaluates, and refines AI-generated content through a pipeline of symbolic operators, ethics checks, quality guardrails, and emotional coherence tracking.

Built by [Revenant Systems LLC](https://github.com/Revenant-Systems-LLC).

---

## What It Does

SAGE-RAGE sits between your application and any LLM provider. It enforces alignment constraints at runtime rather than relying on prompt engineering alone:

- **Ethics enforcement** -- Layer 0 checks on both input and output with lock-on-violation
- **Operator pipeline** -- Containment, Omega (recursion), Chi (coherence), Sigma (skeptical contrast) transforms applied to LLM output
- **Quality guardrails** -- Profile-based checks (casual, professional, technical, creative) with configurable thresholds
- **Emotional modeling** -- Bidirectional VAD (valence/arousal/dominance) tracking with glyph projection
- **Proxy mode** -- Drop-in HTTP proxy that aligns any OpenAI-compatible API without modifying the downstream application

## Architecture

```
SAIGE-RAGE.sln
|
|-- src/
|   |-- SageRage.Core        Core library (operators, providers, ethics, QC, metrics)
|   |-- SageRage.Console      Interactive console app (text, audio, proxy modes)
|   |-- SageRage.Cli          CLI tool (stub -- planned)
|   |-- SageRage.Proxy        HTTP alignment proxy server
|
|-- tests/
|   |-- SageRage.Core.Tests   Unit tests for engine, providers, guardrails, QC
|   |-- SageRage.Proxy.Tests  Unit + integration tests for proxy pipeline
|
|-- prompt/persona/            Agent persona files (Keystone, V, etc.)
|-- .env.example               Template for API keys
```

### Core Components

| Component | Purpose |
|---|---|
| `RageEngine` | Executes operator sequences (Containment, Omega, Chi, Sigma) over `SageState` |
| `SageAgent` | Conversational agent with ethics + operator pipeline integration |
| `SageRuntime` | Runtime state management and operator dispatch |
| `ProxyPipeline` | Runs intercepted proxy content through the full alignment stack |
| `ProxyServer` | Kestrel HTTP server -- aligns any OpenAI-compatible API |
| `SageEmotionalTracker` | VAD emotional state tracking with glyph projection |
| `SageMetrics` | Coherence, entropy, and similarity measurement |
| `SecretLoader` | Secure API key loading from encrypted/locked drives |

### Symbolic Operators

The engine operates on a shared state (`SageState { Text, Emotion, Coherence, Entropy, Trace, ... }`):

| Operator | Symbol | Function |
|---|---|---|
| Containment | `[...]` | Bounds the response within ethical/topical constraints |
| Omega | `Omega` | Recursive refinement -- re-evaluates output for depth |
| Chi | `Chi` | Coherence verification -- ensures logical consistency |
| Sigma | `Sigma` | Skeptical contrast -- challenges assumptions and bias |

Pipeline flow: `[...] -> Omega -> Chi -> Sigma` (configurable per profile).

### LLM Providers

| Provider | Class | Models |
|---|---|---|
| Google Gemini | `GeminiLLMProvider` | gemini-2.0-flash, gemini-2.0-flash-lite |
| Gemini Live Audio | `GeminiLiveAudioProvider` | Native audio streaming via WebSocket |
| OpenAI | `OpenAIProvider` | gpt-4o, gpt-4o-mini |
| Anthropic / Claude | `AnthropicProvider` | claude-sonnet-4-20250514, claude-haiku-4-20250414 |
| OpenAI-Compatible | `OpenAICompatibleProvider` | Any local server (LM Studio, etc.) |
| Ollama | `OllamaProvider` | Any Ollama model tag |

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- At least one LLM provider API key (or a local model server)

### Build

```bash
dotnet build SAIGE-RAGE.sln
```

### First Run

```bash
dotnet run --project src/SageRage.Console
```

On first launch, the setup wizard will prompt you to configure your secrets:

1. **Secrets directory** -- where your `.env` file lives (default: `B:\secrets`)
2. **Profile name** -- which `.env` file to load (default: `SageRage`)
3. If the file doesn't exist, the wizard offers to create one from `.env.example`

After setup, the app presents:
- **Persona selection** -- Delta, Karne, Keystone, Noir, or V
- **Mode selection** -- Text chat, Audio (Gemini Live), or Proxy
- **Provider selection** -- Gemini, OpenAI, Claude, LM Studio, or Ollama

### Re-run Setup

```bash
dotnet run --project src/SageRage.Console -- --setup
```

### Run Tests

```bash
dotnet test SAIGE-RAGE.sln
```

51 tests across two projects (26 core + 25 proxy).

## Proxy Mode

The alignment proxy sits between autonomous AI agents and upstream LLM APIs. The agent connects to `localhost` with no API key. The proxy holds the real key and runs the alignment pipeline on every request/response.

### How It Works

```
[AI Agent] ---> http://localhost:9443/v1/chat/completions (no auth)
                        |
                  [SAGE-RAGE Proxy]
                   1. Ethics check on input
                   2. Forward to upstream (with real API key)
                   3. Run operator pipeline on response
                   4. Guardrail quality checks
                   5. Return aligned response
                        |
               [Upstream API] <--- Bearer <real-key>
               (OpenAI, Gemini, xAI, OpenRouter, etc.)
```

### Security Model

API keys are stored on a lockable encrypted drive (`B:\secrets\SageRage.env`). The proxy loads keys into process memory at startup. Once loaded, the drive can be locked again. Autonomous agents never see, touch, or have access to any API key.

- Keys exist only in the proxy process memory
- The proxy injects auth headers on forwarded requests
- Agents connect to localhost with no credentials
- The `SecretLoader` reads from `.env` files, never from system environment variables

### Endpoints

| Endpoint | Method | Purpose |
|---|---|---|
| `/health` | GET | Health check with config summary |
| `/v1/chat/completions` | POST | Aligned chat completions (streaming + non-streaming) |
| `/v1/completions` | POST | Same handler as chat completions |
| `/v1/{**rest}` | ANY | Pass-through to upstream |

### Trace Headers

Aligned responses include diagnostic headers:

- `X-SageRage-Pipeline` -- `active` or `bypass`
- `X-SageRage-Guardrail` -- `passed` or `flagged`
- `X-SageRage-RequestId` -- Sequential request ID

## Secrets Management

SAGE-RAGE uses a secure key loading system designed to keep API keys away from AI agents and out of system environment variables.

### PowerShell Loader (for manual use)

```powershell
Load-Secrets              # List available .env profiles
Load-Secrets SageRage     # Load keys into current terminal session
Unload-Secrets SageRage   # Clear keys from session
```

Keys are loaded into the current process only and disappear when the terminal closes.

### App-Level Loader (for proxy mode)

The proxy reads keys from the configured `.env` file at startup using `SecretLoader`. Configuration is saved to `%LOCALAPPDATA%\SageRage\secrets.json` so it only needs to be set once.

### Setup

See `.env.example` for all supported key names. Copy it to your secrets directory and fill in your values.

## Project Status

See `RevSys_OfficialUPD.md` for the full implementation roadmap.

| Area | Status |
|---|---|
| Symbolic operator runtime | Implemented |
| Ethics enforcement (Layer 0) | Implemented |
| Quality control / guardrails | Implemented |
| Emotional modeling (VAD) | Implemented |
| Multi-provider support | Implemented (Gemini, OpenAI, Claude, Ollama, OpenAI-compatible) |
| Alignment proxy | Implemented |
| Proxy test suite | Implemented (25 tests) |
| Secure key management | Implemented |
| Audio integration (Gemini Live) | Partial (audio path exists, not yet running full pipeline) |
| CLI tool | Stub |
| Persistent state (Mnemosyne) | Partial |
| Operational autonomy | Not implemented (future research) |

## License

Proprietary. Copyright Revenant Systems LLC.
