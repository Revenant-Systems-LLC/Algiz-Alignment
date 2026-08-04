# SAIGE-RAGE (Algiz)

**Secure AI Guardrail Enforcement (SAIGE) powered by Revenant Alignment Governance Engine (RAGE)**

Runtime guardrails and audit trail around LLM calls. Built by David Fisher at [Revenant Systems LLC](https://github.com/Revenant-Systems-LLC).

> **Ground truth:** [docs/REALITY-MAP.md](docs/REALITY-MAP.md) · **Short honest paper:** [docs/Algiz-Current-Position.md](docs/Algiz-Current-Position.md).  
> Word whitepaper drafts under `docs/` are historical. They are not current architecture claims.

---

## What it is (honest)

Algiz sits between your application (or a local proxy) and an LLM provider. Today it **reliably** provides:

- **Quality checks** on drafts (citations, completeness, basic claim sanity, simple grounding overlap when anchors exist)
- **Ethical / policy gating** (layered stack; strength depends on policy content)
- **An audit event trail** for governed requests (in-memory today; see limitations)
- **Multi-provider LLM adapters** and an **OpenAI-compatible alignment proxy**
- **Emotional VAD signals** and a derived **Malice** score used in governance status

It also **contains** experimental pipeline stages (context bound, refine loop, multi-temp select, skeptical revise). Those stages **run in code** but are **not yet load-bearing product features**: they have not consistently been shown to change outcomes for the better under the criteria in `docs/REALITY-MAP.md`.

It is **not** a finished formal “operator algebra,” not a proof of alignment, and not a replacement for provider safety systems.

Older docs and the whitepaper still use Greek letters and operator brands (Omega, Chi, Sigma, etc.). **Those names are not used as product claims here** until the matching stage is load-bearing. Code enums may still use the old names.

---

## What it does

| Capability | Status |
|---|---|
| QC / guardrail checks | **In use** |
| Ethical layers on input/output | **In use** (policy-dependent) |
| Audit trail API (`/api/audit/*`) | **In use**, weak persistence |
| Alignment HTTP proxy | **In use** |
| Multi-provider generate | **In use** |
| Context / memory bounding | Present; limited |
| Refinement loop | Present; often near-copy “convergence” |
| Best-of-N selection | Present; often ranks a set of size one |
| Skeptical revise vs memory | Present; thin |
| Ossuary RAG / NLI gates / hash-chained constraint ledger | **Not in this repo** |

---

## Runtime flow (as implemented)

```
User input
   |
   v
Agent / proxy entry
   |
   +--> Ethical gate (input)
   +--> Optional emotion update
   +--> Optional memory retrieve
   +--> Prompt assembly
   +--> LLM draft
   +--> Pipeline stages (when configured):
   |      context bound
   |      refinement loop
   |      multi-temp select   (does not earn "best-of-N" until candidates differ by approach)
   |      skeptical revise    (high-stakes paths)
   +--> Quality checks
   +--> Ethical gate (output)
   +--> Audit record
   v
Response
```

Default sequences in code still call the old stage names internally. Externally, prefer the engineering names above.

---

## Core components

| Component | Purpose |
|---|---|
| `GovernedEngine` / `SageAgent` | Orchestration: draft, gates, QC, status |
| `RageEngine` | Optional pipeline stages over `SageState` |
| `QualityControl` / guardrail checks | Real veto/flag style checks |
| `AuditLedgerService` | In-memory audit events + summary |
| `ProxyPipeline` / `ProxyServer` | OpenAI-compatible proxy alignment path |
| `SageMetrics` | Similarity and perplexity **proxies** (not ground truth) |
| `SecretLoader` | API keys from configured secrets dir, not process env by default |
| LLM providers | Gemini, OpenAI, Anthropic, Ollama, OpenAI-compatible |

### Solution layout

```
SAIGE-RAGE.sln
|-- src/SageRage.Core
|-- src/SageRage.Proxy
|-- src/SageRage.Api
|-- src/SageRage.Cli
|-- src/SageRage.Console
|-- src/SageRage.UI
|-- Tests/
|-- docs/          REALITY-MAP.md, whitepaper drafts, notes
|-- scripts/       experiments (not product)
```

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- At least one LLM provider API key (or a local model server)

### Build

```bash
dotnet build SAIGE-RAGE.sln
```

### Console

```bash
dotnet run --project src/SageRage.Console
```

On first launch, the setup wizard configures secrets (directory + profile). Default secrets path has historically been a locked drive path; confirm on your machine before relying on it.

### Tests

```bash
dotnet test SAIGE-RAGE.sln
```

### Eval CLI (local example)

```bash
dotnet run --project src/SageRage.Cli -- eval --dataset src/SageRage.Cli/sample_dataset.json --provider Ollama
```

---

## Proxy mode

The alignment proxy sits between a client and an upstream OpenAI-compatible API. The client talks to localhost; the proxy holds credentials and runs gates/QC on the path.

```
[Client] --> http://localhost:9443/v1/chat/completions
                    |
              [SAIGE-RAGE Proxy]
               ethics on input
               forward upstream
               pipeline + QC on response
               return result
                    |
            [Upstream API]
```

Aligned responses may include headers such as:

- `X-SageRage-Pipeline`
- `X-SageRage-Guardrail`
- `X-SageRage-RequestId`

Keys are loaded via `SecretLoader` from a configured `.env` profile. See `.env.example`.

---

## Project status

| Area | Status |
|---|---|
| QC / guardrails | Implemented and used |
| Ethics layers | Implemented |
| Audit trail | Implemented, in-memory only |
| Multi-provider support | Implemented |
| Alignment proxy | Implemented |
| Emotional VAD + Malice | Implemented (proxy metric) |
| Context bound | Partial |
| Refinement loop | Experimental (not load-bearing) |
| Multi-temp selection | Experimental (not load-bearing) |
| Skeptical revise | Thin |
| Persistent / hash-chained ledger | Not done |
| Retrieval over company corpus | Not in this repo |
| Whitepaper vs code | **Diverged**; trust REALITY-MAP + code |
| Evaluation benchmarks | Early / planned |

---

## Limitations

- Prototype. Not a solved alignment system.
- Does not make the base model truthful or safe by itself.
- Similarity, perplexity, and Malice are **proxies**.
- Audit trail is not durable and can drop old events under the 750-event cap.
- Refinement and selection stages can run without improving the answer; do not market them as proven.
- Policy quality dominates ethical gate behavior.

---

## Near-term engineering order

1. Keep docs and demos aligned with [REALITY-MAP.md](docs/REALITY-MAP.md) (this pass).
2. Make multi-hypothesis drafting real (**distinct approaches**, not wider temperature noise on one rewrite prompt) before calling anything “best-of-N.”
3. Only then choose a selection score against a small labeled preference set.
4. Persistence before any hash-chained ledger story.
5. Retrieval only with an explicit corpus config (and explicit exclusions), never implied by operator mythology.

---

## Documentation

| Doc | Role |
|---|---|
| [docs/REALITY-MAP.md](docs/REALITY-MAP.md) | **What exists** |
| Whitepaper `.docx` under `docs/` | Historical / research narrative; may overclaim |
| [docs/TERMINOLOGY-MAP.md](docs/TERMINOLOGY-MAP.md) | Literature naming map for draft 5; not a load-bearing certificate |
| [docs/CFO-DEMO-KIT.md](docs/CFO-DEMO-KIT.md) | Demo language; update before external use |

---

## License

Proprietary. Copyright Revenant Systems LLC.
