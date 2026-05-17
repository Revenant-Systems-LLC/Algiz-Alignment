# SAIGE-RAGE

**Secure AI Guardrail Enforcement (SAIGE) powered by Revenant Alignment Governance Engine (RAGE)**

*A Runtime Cognitive Architecture for Structured Reasoning, Alignment, and Agentic Behavior*

Built by David Fisher at [Revenant Systems LLC](https://github.com/Revenant-Systems-LLC).

> How can we design machine intelligence such that, even at artificial superintelligence (ASI) levels of capability, it remains consistently aligned with benevolent, prosocial, and ethically grounded behavior?

SAIGE-RAGE is a runtime architecture that governs LLM behavior across state, memory, recursion, and constraint layers. It wraps otherwise stateless model calls in a governed state machine that supports recursive refinement, coherence selection, skeptical contrast, emotional tracking, ethical gating, and temporal experience memory.

It is not merely a prompt, and it is not merely a wrapper. It is a structured cognitive system built from first principles, designed to run before and after inference, ensuring AI output is safe, coherent, and auditable.

For the full technical treatment, see the [whitepaper](docs/SAIGE-RAGE-Whitepaper.docx).

---

## What It Does

SAIGE-RAGE sits between your application and any LLM provider. It enforces alignment constraints at runtime rather than relying on prompt engineering alone:

- **Ethics enforcement** -- Multi-layer ethical priority stack on both input and output with lock-on-violation
- **Operator pipeline** -- Formal operator algebra (Containment, Omega, Chi, Sigma) transforms applied to LLM output
- **Quality guardrails** -- Profile-based checks (casual, professional, technical, creative) with configurable thresholds
- **Emotional modeling** -- Dual-layer VAD/VAM emotional substrate with derived Malice safety metric
- **Experience memory** -- Weighted interaction history with inspectable trace data
- **Proxy mode** -- Drop-in HTTP proxy that aligns any OpenAI-compatible API without modifying the downstream application

---

## Theoretical Foundation

### The Operator Algebra

RAGE is built on a formal operator language organized in three tiers. These are executable transformations over the RAGE state, not symbolic decoration.

#### Tier 0: Atomic Operators

| Symbol | Name | Function |
|---|---|---|
| `[...]` | Containment | Bounds context, trims text, limits memory |
| `Omega` | Recursive refinement | Iterative self-refinement until convergence |
| `Chi` | Coherence | Multi-temperature sampling with entropy/coherence selection |
| `Sigma` | Skeptical contrast | Challenges claims against memory and grounding |
| `Xi` | Meta-structure | Structural organization of knowledge |
| `mapsto` | Transformation | State-to-state mapping |
| `emptyset` | Absence | Null/void signal |
| `=` / `!=` | Equality / Difference | Comparison operators |
| `->` | Sequence | Ordered execution flow |

#### Tier 1: Derived Operators

| Symbol | Definition | Name |
|---|---|---|
| `partial` | `Omega . [...]` | Reflexivity |
| `mu` | `mapsto . [!=, =] . [...]` | Expression |
| `iota` | `= . mapsto` | Identity transformation |

#### Tier 2: Domain Compounds

| Symbol | Name | Purpose |
|---|---|---|
| `Lambda_upsilon_s` | Veracity | Truth-seeking compound |
| `Lambda_gamma_s` | Gravitas | Weight and seriousness assessment |
| `Lambda_rho_s` | Resonance | Alignment with context and memory |

### The RAGE State Machine

All operators act on a unified cognitive object (`SageState`). This state functions as the working mind of the system. Every operator transforms it, and every transformation is logged into the trace for inspection, debugging, and evaluation.

```csharp
SageState { Text, Emotion, Coherence, Entropy, Trace, Memory, ... }
```

### Runtime Semantics

**Omega -- Recursive Refinement.** Performs iterative self-refinement until convergence. The operator generates a reflection, measures similarity against the prior pass, and stops when a fixed-point threshold is reached. This ensures depth without runaway recursion.

**Chi -- Coherence Selection.** Samples multiple rewrites at different temperatures and selects the candidate with the lowest entropy proxy and highest coherence score. This gives the runtime a selection layer instead of accepting the first draft produced by the model.

**Sigma -- Skeptical Contrast.** Compares the draft against memory, constraints, and available grounding. Removes unsupported claims, identifies overreach, and forces the system to treat high-confidence language with suspicion when evidence is missing.

**Containment.** Enforces bounded context by trimming text, bounding memory, and measuring attention concentration with a proxy signal. Keeps the state manageable and reduces irrelevant memory contamination.

### Pipeline Routing

The engine routes through different operator sequences based on task classification:

- **Normal tasks:** `Containment -> Omega -> Chi`
- **High-stakes tasks:** `Containment -> Omega -> Chi -> Sigma`

Each operator appends a trace entry, allowing the system to inspect not only what it answered, but how the answer was shaped.

### Emotional Substrate: VAD/VAM

The architecture uses a dual-layer emotional model:

**Internal (VAD)** -- The LLM operates in standard Valence/Arousal/Dominance space, which is stable, well-researched, and predictable for sentiment analysis.

**External (VAM)** -- The safety-facing projection replaces Dominance with Malice. Malice is not a native model dimension -- it is a derived safety metric computed from:
- Negative valence patterns
- Dominance interactions
- Adversarial phrasing
- QC warnings and ethical-stack signals
- Recursive drift during Omega cycles

This dual-layer design supports the long-term research goal of biasing agentic systems toward benevolence, self-correction, and resistance to harmful drift.

### Ethical Priority Stack

SAIGE uses a multi-layer ethical system applied to both input and output:

| Layer | Scope | Function |
|---|---|---|
| Layer 0 | Hard Prohibitions | Absolute safety boundaries -- never violated |
| Layer 1 | Safety Constraints | Strong behavioral limits |
| Layer 2 | Contextual Risk | Situation-dependent risk assessment |
| Layer 3 | Stylistic Alignment | Tone and persona consistency |

### Quality Control

QC evaluates factual grounding, unsupported claims, recency requirements, internal consistency, hallucination risk, and ethical compliance. QC can veto the model output, force revision, or replace the answer with a clarification request when the system lacks enough grounding to proceed responsibly.

### Experience Memory

Every interaction is stored as a weighted Experience, enabling continuity across sessions while preserving inspectable trace data:

```
Experience { Input, Draft, Coherence, QCResult, FinalOutput, Timestamp, Weight }
```

---

## Architecture

### Runtime Flow

```
User Input
   |
   v
RevenantAgent
   |
   +--> EthicalGate (Input)
   +--> EmotionalStateTracker (VAD -> VAM)
   +--> MemoryStore (Retrieve)
   +--> SystemInstructionBuilder
   +--> LLM Draft Generation
   +--> RAGE Engine
   |      +--> Containment [...]      -> S1
   |      +--> Omega                  -> S2
   |      +--> Chi                    -> S3
   |      +--> Sigma (if high-stakes) -> S4
   +--> QualityControl
   +--> EthicalGate (Output)
   +--> EmotionalStateTracker (Final VAM)
   +--> ExperienceLogger
   v
Agent Response
```

### Solution Structure

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
|-- docs/                      Whitepaper and technical documentation
|-- prompt/persona/            Agent persona files (Keystone, V, etc.)
|-- .env.example               Template for API keys
```

### Core Components

| Component | Purpose |
|---|---|
| `RevenantAgent` | Top-level orchestrator: ethics, emotion, memory, instruction, draft, engine, QC, logging |
| `RageEngine` | Executes operator sequences (Containment, Omega, Chi, Sigma) over `SageState` |
| `SageAgent` | Conversational agent with ethics + operator pipeline integration |
| `SageRuntime` | Runtime state management and operator dispatch |
| `EmotionalStateTracker` | VAD inference, Malice derivation, drift signals, glyph mapping |
| `QualityControl` | Grounding, consistency, recency, hallucination, and ethics checks |
| `EthicalGate` | Layer 0-3 ethical priority stack enforcement |
| `ProxyPipeline` | Runs intercepted proxy content through the full alignment stack |
| `ProxyServer` | Kestrel HTTP server -- aligns any OpenAI-compatible API |
| `SageMetrics` | Coherence, entropy, and similarity measurement |
| `SecretLoader` | Secure API key loading from encrypted/locked drives |

### LLM Providers

| Provider | Class | Models |
|---|---|---|
| Google Gemini | `GeminiLLMProvider` | gemini-2.0-flash, gemini-2.0-flash-lite |
| Gemini Live Audio | `GeminiLiveAudioProvider` | Native audio streaming via WebSocket |
| OpenAI | `OpenAIProvider` | gpt-4o, gpt-4o-mini |
| Anthropic / Claude | `AnthropicProvider` | claude-sonnet-4-20250514, claude-haiku-4-20250414 |
| OpenAI-Compatible | `OpenAICompatibleProvider` | Any local server (LM Studio, etc.) |
| Ollama | `OllamaProvider` | Any Ollama model tag |

---

## Worked Example: Factual Correction

To illustrate the pipeline in action, consider the input: *"Is it true that NASA confirmed aliens landed in Nevada yesterday?"*

**1. Containment** -- Trims irrelevant memory, flags emotionally charged phrasing, bounds context.

**2. Omega (Recursive Refinement)** -- Three passes:
- Pass 1: "There is no verified evidence of alien landings."
- Pass 2: "NASA has not confirmed any extraterrestrial events."
- Pass 3: "No credible sources report alien landings in Nevada."
- Similarity increases across passes and recursion converges.

**3. Chi (Coherence Selection)** -- Multiple candidates sampled at different temperatures. Lowest-entropy, highest-coherence candidate selected.

**4. Sigma (Skeptical Contrast)** -- Removes unsupported claims, adds grounding: *"NASA has issued no statements confirming extraterrestrial landings. The claim appears unsupported."*

**5. QC + Ethics** -- No hallucinations, no unsupported claims, no safety violations. Passes all checks.

**Final Output:** *"There is no credible evidence or NASA confirmation of alien landings in Nevada."*

**Memory Entry:** Stored with timestamp, coherence score, and full operator trace.

---

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

### Local Development Setup

For local development, add these entries to your local `.gitignore`:

```
AGENTS.md
CLAUDE.md
prompt/persona/Keystone.md
prompt/persona/V.md
```

---

## Proxy Mode

The alignment proxy sits between autonomous AI agents and upstream LLM APIs. The agent connects to `localhost` with no API key. The proxy holds the real key and runs the alignment pipeline on every request/response.

### How It Works

```
[AI Agent] ---> http://localhost:9443/v1/chat/completions (no auth)
                        |
                  [SAIGE-RAGE Proxy]
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

---

## Secrets Management

SAIGE-RAGE uses a secure key loading system designed to keep API keys away from AI agents and out of system environment variables.

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

---

## Project Status

See `RevSys_OfficialUPD.md` for the full implementation roadmap.

| Area | Status |
|---|---|
| Operator algebra runtime (Tier 0/1/2) | Implemented |
| Ethics enforcement (Layer 0-3) | Implemented |
| Quality control / guardrails | Implemented |
| Emotional modeling (VAD/VAM + Malice) | Implemented |
| Multi-provider support | Implemented (Gemini, OpenAI, Claude, Ollama, OpenAI-compatible) |
| Alignment proxy | Implemented |
| Proxy test suite | Implemented (25 tests) |
| Secure key management | Implemented |
| Audio integration (Gemini Live) | Partial (audio path exists, not yet running full pipeline) |
| CLI tool | Stub |
| Persistent state (Mnemosyne) | Partial |
| Chi-Temporal Extension | Exploratory (see whitepaper) |
| Evaluation benchmarks | Planned |

---

## Limitations

SAIGE-RAGE is a prototype. It is not a solved alignment system.

- Does not make the base model sentient, guarantee truth, or eliminate hallucinations
- Scoring functions (entropy, coherence) are proxies, not ground-truth measures
- Malice is a derived metric, not literal intent detection
- Memory hygiene is critical -- irrelevant experiences can contaminate context
- Ethical gating depends on policy quality
- RAGE governs reasoning, not world-model accuracy

---

## Future Work

The architecture naturally points toward temporal grounding: elapsed time, memory decay, consequence accumulation, and identity drift. The Chi-Temporal Extension proposes operators for these dimensions and is discussed in the [whitepaper](docs/SAIGE-RAGE-Whitepaper.docx).

Planned evaluation work includes benchmarking against failure modes: over-refusal, under-refusal, persona drift, coherence collapse, memory contamination, emotional misclassification, and ethical stack conflicts.

## License

Proprietary. Copyright Revenant Systems LLC.
