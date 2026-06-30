# ᛉ ALGIZ ALIGNMENT ENGINE

**Secure AI Guardrail Enforcement (SAGE)** and the  
**Revenant Alignment Governance Engine (RAGE)**

### Enterprise Runtime Governance for Safe, Reliable, and Auditable AI

Built by David Fisher at [Revenant Systems LLC](https://github.com/Revenant-Systems-LLC).

---

## Executive Summary

Algiz Alignment Engine is a runtime governance layer that sits between enterprise applications and LLM providers.  
It enforces policy, quality, and safety constraints **before and after inference**, with traceable artifacts for inspection and audit.

This is not prompt engineering alone.  
This is not a lightweight wrapper.  
This is a governed runtime designed to make AI output safer, more reliable, and operationally accountable in production systems.

For executive teams (CIO, CFO, CISO, COO), Algiz is built to reduce downside risk while improving deployment confidence and model-provider flexibility.

---

## Why Enterprises Use Algiz

- **Reduce risk exposure** from unsafe, non-compliant, or low-confidence model output
- **Improve reliability** with multi-stage runtime refinement and quality control
- **Increase auditability** with structured traces across decision stages
- **Control cost of failure** by reducing remediation, escalation, and manual correction
- **Avoid vendor lock-in** with provider-agnostic integration paths

---

## The Problem It Solves

Most AI deployments still depend heavily on prompting and post-hoc review. That often breaks under real production pressure:

- Prompt adherence degrades across long or shifting context
- Safety checks happen too late (after risky output is generated)
- Behavior is inconsistent across model providers
- Autonomous workflows can drift without runtime constraints

Algiz addresses this with enforceable runtime governance.

---

## What Algiz Does

Algiz enforces alignment and guardrails through coordinated subsystems:

- **Ethics enforcement** — Multi-layer policy stack on both input and output, with lock-on-violation behavior
- **Operator pipeline** — Formal operator sequence (`Containment`, `Omega`, `Chi`, `Sigma`) applied to runtime state
- **Quality control** — Profile-based checks (casual, professional, technical, creative) with configurable thresholds
- **Emotional safety modeling** — Dual-layer VAD/VAM substrate with derived **Malice** safety metric
- **Experience memory** — Weighted interaction history with inspectable trace lineage
- **Proxy mode** — Drop-in OpenAI-compatible HTTP proxy to govern existing apps without major rewrites

---

## Business Outcomes by Stakeholder

### CFO / Finance
- Lower expected loss from AI-related output incidents
- Reduced remediation and human-review burden
- Better predictability of AI operational cost

### CIO / CTO
- Governance layer decoupled from specific model vendors
- Faster internal approval for production rollouts
- Better visibility into quality and safety behavior

### CISO / Risk
- Explicit policy boundaries with enforceable gate behavior
- Runtime controls for higher-risk routes and use cases
- Stronger audit posture than prompt-only methods

### COO / Operations
- More consistent output quality across teams
- Lower variance in workflow outcomes
- Higher confidence in scaled automation

---

## Why Runtime Governance vs Prompt-Only Guardrails

| Capability | Prompt-Only | Algiz (SAGE-RAGE Runtime) |
|---|---|---|
| Pre-inference risk gating | Limited | Yes |
| Post-inference veto/revision | Limited | Yes |
| Structured multi-stage transformation | No | Yes |
| Decision traceability | Minimal | Yes |
| Provider-agnostic enforcement | Partial | Yes |
| High-stakes routing hardening | Weak | Yes |

---

## 2-Week Pilot Path

A low-friction pilot can validate value quickly.

### Week 1 — Observe
- Deploy proxy in monitor mode
- Mirror selected workflow(s)
- Capture baseline: flags, revisions, unsupported-claim patterns

### Week 2 — Enforce
- Activate policy and QC enforcement on selected routes
- Enable revise/veto behavior for high-risk paths
- Compare outcomes against baseline

### Suggested Pilot KPIs
- Unsafe/non-compliant outputs blocked
- Unsupported-claim rate reduction
- Human intervention reduction
- Output consistency improvement

---

## Architecture

### Runtime Flow

```text
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

```text
SAGE-RAGE.sln
|
|-- src/
|   |-- SageRage.Core         Core library (operators, providers, ethics, QC, metrics)
|   |-- SageRage.Console      Interactive console app (text, audio, proxy modes)
|   |-- SageRage.Cli          CLI tool (stub -- planned)
|   |-- SageRage.Proxy        HTTP alignment proxy server
|
|-- tests/
|   |-- SageRage.Core.Tests   Unit tests for engine, providers, guardrails, QC
|   |-- SageRage.Proxy.Tests  Unit + integration tests for proxy pipeline
|
|-- docs/                     Whitepaper and technical documentation
|-- prompt/persona/           Agent persona files (Keystone, V, etc.)
|-- .env.example              Template for API keys
```

### Core Components

| Component | Purpose |
|---|---|
| `RevenantAgent` | Top-level orchestration: ethics, emotion, memory, instruction, draft, engine, QC, logging |
| `RageEngine` | Executes operator sequences over `SageState` |
| `SageAgent` | Conversational agent with ethics + operator pipeline integration |
| `SageRuntime` | Runtime state management and operator dispatch |
| `EmotionalStateTracker` | VAD inference, Malice derivation, drift signals, glyph mapping |
| `QualityControl` | Grounding, consistency, recency, hallucination, ethics checks |
| `EthicalGate` | Layer 0–3 ethical priority stack enforcement |
| `ProxyPipeline` | Runs intercepted proxy content through full alignment stack |
| `ProxyServer` | Kestrel server that aligns OpenAI-compatible API traffic |
| `SageMetrics` | Coherence, entropy, and similarity measurement |
| `SecretLoader` | Secure API key loading from encrypted/locked drives |

---

## Theoretical Foundation

RAGE is built on a formal operator language in three tiers.  
These operators are executable transformations over runtime state—not symbolic decoration.

```csharp
SageState { Text, Emotion, Coherence, Entropy, Trace, Memory, ... }
```

### Tier 0: Atomic Operators

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

### Tier 1: Derived Operators

| Symbol | Definition | Name |
|---|---|---|
| `partial` | `Omega . [...]` | Reflexivity |
| `mu` | `mapsto . [!=, =] . [...]` | Expression |
| `iota` | `= . mapsto` | Identity transformation |

### Tier 2: Domain Compounds

| Symbol | Name | Purpose |
|---|---|---|
| `Lambda_upsilon_s` | Veracity | Truth-seeking compound |
| `Lambda_gamma_s` | Gravitas | Weight and seriousness assessment |
| `Lambda_rho_s` | Resonance | Alignment with context and memory |

---

## Runtime Semantics

- **Containment** — Enforces bounded context and memory hygiene
- **Omega** — Performs iterative refinement until convergence
- **Chi** — Selects candidates using coherence + entropy signals
- **Sigma** — Applies skeptical contrast on high-stakes paths

### Pipeline Routing

- **Normal tasks:** `Containment -> Omega -> Chi`
- **High-stakes tasks:** `Containment -> Omega -> Chi -> Sigma`

Each operator appends trace entries, allowing inspection of both outcome and reasoning path.

---

## Emotional Substrate (VAD/VAM)

Algiz uses a dual-layer emotional model:

- **Internal (VAD)** — Valence/Arousal/Dominance operating space
- **External (VAM)** — Safety-facing projection replacing Dominance with **Malice**

Malice is a derived signal computed from:
- Negative valence patterns
- Dominance interactions
- Adversarial phrasing
- QC warnings and ethics signals
- Recursive drift during `Omega` cycles

---

## Ethical Priority Stack

| Layer | Scope | Function |
|---|---|---|
| Layer 0 | Hard Prohibitions | Absolute safety boundaries (never violated) |
| Layer 1 | Safety Constraints | Strong behavioral limits |
| Layer 2 | Contextual Risk | Situation-dependent risk assessment |
| Layer 3 | Stylistic Alignment | Tone and persona consistency |

---

## Quality Control

QC evaluates:
- Factual grounding
- Unsupported claims
- Recency requirements
- Internal consistency
- Hallucination risk
- Ethical compliance

QC can pass, force revision, veto, or replace output depending on configured thresholds and policy conditions.

---

## Experience Memory

Each interaction is stored as a weighted Experience with trace context:

```text
Experience { Input, Draft, Coherence, QCResult, FinalOutput, Timestamp, Weight }
```

This supports continuity across sessions while preserving inspectability.

---

## LLM Provider Support

| Provider | Class | Models |
|---|---|---|
| Google Gemini | `GeminiLLMProvider` | `gemini-2.0-flash`, `gemini-2.0-flash-lite` |
| Gemini Live Audio | `GeminiLiveAudioProvider` | Native audio streaming via WebSocket |
| OpenAI | `OpenAIProvider` | `gpt-4o`, `gpt-4o-mini` |
| Anthropic / Claude | `AnthropicProvider` | `claude-sonnet-4-20250514`, `claude-haiku-4-20250414` |
| OpenAI-Compatible | `OpenAICompatibleProvider` | Any local server (LM Studio, etc.) |
| Ollama | `OllamaProvider` | Any Ollama model tag |

---

## Proxy Mode

The alignment proxy sits between autonomous agents and upstream LLM APIs.  
Agents connect locally; proxy applies governance and injects upstream auth.

### How It Works

```text
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

- Keys stored on lockable encrypted drive (`B:\secrets\SageRage.env`)
- Keys loaded into proxy process memory at startup
- Drive can be re-locked after startup
- Agents never see real API keys
- `SecretLoader` reads `.env` profiles, not system-wide environment variables

### Endpoints

| Endpoint | Method | Purpose |
|---|---|---|
| `/health` | GET | Health check + config summary |
| `/v1/chat/completions` | POST | Aligned chat completions (streaming + non-streaming) |
| `/v1/completions` | POST | Same handler as chat completions |
| `/v1/{**rest}` | ANY | Pass-through to upstream |

### Trace Headers

- `X-SageRage-Pipeline` — `active` or `bypass`
- `X-SageRage-Guardrail` — `passed` or `flagged`
- `X-SageRage-RequestId` — Sequential request ID

---

## Worked Example (Factual Correction)

Input:  
*"Is it true that NASA confirmed aliens landed in Nevada yesterday?"*

1. **Containment** — Bounds context; identifies emotionally charged phrasing  
2. **Omega** — Recursive refinements converge toward stable correction  
3. **Chi** — Chooses best candidate by coherence/entropy profile  
4. **Sigma** — Removes unsupported claims; reinforces grounding  
5. **QC + Ethics** — Verifies policy and quality compliance

Final output style:  
*"There is no credible evidence or NASA confirmation of alien landings in Nevada."*

---

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- At least one LLM provider API key (or a local model server)

### Build

```bash
dotnet build SAGE-RAGE.sln
```

### First Run

```bash
dotnet run --project src/SageRage.Console
```

On first launch, setup prompts for:
1. **Secrets directory** — where your `.env` file lives (default: `B:\secrets`)
2. **Profile name** — which `.env` file to load (default: `SageRage`)
3. Optional `.env` creation from `.env.example` if missing

Then choose:
- **Persona** — Delta, Karne, Keystone, Noir, or V
- **Mode** — Text chat, Audio (Gemini Live), or Proxy
- **Provider** — Gemini, OpenAI, Claude, LM Studio, or Ollama

### Re-run Setup

```bash
dotnet run --project src/SageRage.Console -- --setup
```

### Run Tests

```bash
dotnet test SAGE-RAGE.sln
```

51 tests across two projects (26 core + 25 proxy).

### Local Development Setup

For local development, add these entries to your local `.gitignore`:

```gitignore
AGENTS.md
CLAUDE.md
```

---

## Validation & Project Status

See `RevSys_OfficialUPD.md` for implementation roadmap details.

| Area | Status |
|---|---|
| Operator algebra runtime (Tier 0/1/2) | Implemented |
| Ethics enforcement (Layer 0–3) | Implemented |
| Quality control / guardrails | Implemented |
| Emotional modeling (VAD/VAM + Malice) | Implemented |
| Multi-provider support | Implemented (Gemini, OpenAI, Claude, Ollama, OpenAI-compatible) |
| Alignment proxy | Implemented |
| Proxy test suite | Implemented (25 tests) |
| Secure key management | Implemented |
| Audio integration (Gemini Live) | Partial (audio path exists, full pipeline pending) |
| CLI tool | Stub |
| Persistent state (Mnemosyne) | Partial |
| Chi-Temporal Extension | Exploratory (see whitepaper) |
| Evaluation benchmarks | Planned |

---

## Limitations

Algiz is a production-oriented prototype, not a claim of solved alignment.

- Does not guarantee universal truth or eliminate hallucinations
- Entropy/coherence are proxy signals, not ground-truth measures
- Malice is derived safety scoring, not literal intent detection
- Memory hygiene and policy quality remain critical
- Runtime governance improves behavior control, not world-model certainty

---

## Future Work

Planned work includes:
- Temporal grounding (elapsed time, memory decay, consequence accumulation, identity drift)
- Benchmarking against failure modes (over-/under-refusal, persona drift, coherence collapse, memory contamination, emotional misclassification, ethical-stack failures)
- Expanded deployment and observability capabilities for enterprise operations

For full technical treatment, see the [whitepaper](docs/Algiz-Alignment-Whitepaper.docx).

---

## Commercial & Evaluation

For enterprise pilot and licensing discussions, contact Revenant Systems LLC.

---

## License

Proprietary. Copyright Revenant Systems LLC.
