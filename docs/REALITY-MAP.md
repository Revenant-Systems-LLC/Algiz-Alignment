# Algiz reality map

**Status:** living source of truth for what exists in code vs what was only proposed.  
**Rule:** no glyph, Greek letter, or operator brand name is product language unless the stage is load-bearing under the criteria below.  
**Updated:** 2026-07-26 (adversarial review: Axiom / Opus / Grok builder seat).

---

## Load-bearing test (all four required)

A pipeline stage may keep a **public name** only if:

1. **Named condition** — it runs under a defined, code-enforced condition  
2. **Observable output** — you can see what it did (trace field, log, test assertion, stdout)  
3. **Stated failure mode** — how it fails and how you detect that  
4. **Changed an outcome at least once** — not merely executed; it altered the answer or a gate decision in a real run or test with evidence  

**Present + executing is not enough.** A selector that ranks a candidate set of size one is not load-bearing selection.

Symbols (Ω, χ, σ, `[...]`, etc.) are **optional nicknames for stages that already pass this test**. They are not a parallel math product language for unfinished work.

---

## What is real today

| Engineering name | Code location (primary) | Load-bearing? | Notes |
|------------------|-------------------------|---------------|--------|
| Input/output ethical gates | `SageAgent` / governance / ethical stack | Partial | Layer 0–3 structure exists; treat claims carefully |
| Quality checks (QC) | `QualityControl/Checks.cs`, guardrail registry | **Yes** | Phantom citations, completeness, claim sanity, grounding overlap, etc. |
| Audit event trail | `AuditLedgerService`, `GET /api/audit/*` | **Yes, weak** | In-memory `ConcurrentQueue`, cap 750, **evicts under load**, lost on restart. Not hash-chained. Not a constraint ledger. |
| LLM provider adapters | `*Provider.cs`, `ILLMProvider` | **Yes** | Multi-provider generate/embed (embed varies by provider) |
| HTTP alignment proxy | `SageRage.Proxy` | **Yes** | Intercepts OpenAI-compatible traffic; runs pipeline + headers |
| Emotional VAD + Malice | `SageEmotionalTracker`, governance | Partial | Implemented with tests; not a full “emotional substrate product” claim |
| Context / memory bound | `RageEngine.ExecuteContainment` | Partial | Trims memory count and snippet length. Not attention isolation. |
| Refinement loop | `RageEngine.ExecuteOmega` | **Not load-bearing yet** | Runs “refine draft” + similarity stop. Often fixed-points by near-copy. Does not yet prove quality gain over single-pass. |
| Best-of-N selection | `RageEngine.ExecuteChi`, `ScoreChiCandidates` | **Not load-bearing yet** | Code runs multi-temp rewrites + score. Candidate set often collapses to one distinct draft under rewrite prompts. Score is uncalibrated (min-max perplexity dominates). |
| Skeptical revise | `RageEngine.ExecuteSigma` | Partial | One critique/revise prompt against memory. Thin. |
| Ossuary RAG index | — | **No** | Not in this repo. `OSSUARY_INDEX.md` is a file tree, not a chunked retrieval index. |
| NLI / deberta gating | — | **No** | Debate artifact only |
| Hash-chained constraint ledger | — | **No** | Demo script only under `scripts/adversarial_defense/` |
| Formal operator algebra (Tier 1/2 compounds) | — | **No as product** | Names in docs/whitepaper; not a verified executable algebra |

---

## Honest one-sentence product

**Algiz runs real quality checks and an audit trail around an LLM call, with optional refinement and multi-temp selection stages that currently are not earning their keep as distinct product capabilities.**

That sentence is less impressive than older whitepaper language. It is the version that survives a repo grep.

---

## Glyph / symbol policy

| Symbol / brand | Product use | Code enum may still exist |
|----------------|-------------|---------------------------|
| Ω / Omega | **Do not** present as proven iterative self-refinement | Yes (`CoreOperator.Omega`) |
| χ / Chi | **Do not** present as proven best-of-N until N≥2 **distinct approaches** and a labeled selection criterion | Yes (`CoreOperator.Chi`) |
| σ / Sigma | **Do not** present as full skeptical verification | Yes (`CoreOperator.Sigma`) |
| Containment / `[...]` | Prefer **context bound** / **memory bound** | Yes |
| Xi, mapsto, partial, Lambda_* compounds | **Docs only / historical** until implemented and load-bearing | No product claim |

When a stage passes the four-part test, it may earn back a short public name (and optionally a glyph). Until then, use engineering names only in README, demos, and external claims.

---

## Candidate generation (selection prerequisite)

Best-of-N is not “three temperatures on the same rewrite prompt.”

- **Wrong success metric:** `unique_hash_count >= 3` at a wider temperature band (manufactures diversity via noise; selection often returns the coolest sample = “just use low T” at 3× cost).  
- **Right success metric:** **distinct approaches** (different framings, system constraints, or deliberate multi-hypothesis prompts), with evidence that selection (or a human label) prefers one for a reason other than “least noisy.”

Temperature must still be correctly wired (`options.temperature` is the Ollama API-contract home; top-level also affects sampling on current Ollama, verified 2026-07-26, but contract cleanup remains good).

---

## Doc / whitepaper debt

| Artifact | Action |
|----------|--------|
| `docs/Algiz-Current-Position.md` | **Current short honest paper** (2026-07-26). Prefer for external claims. |
| `README.md` | Relabeled to match this map (2026-07-26) |
| `CLAUDE.md` / `Agents.md` | Load-bearing four-part test at session start (2026-07-26) |
| `docs/TERMINOLOGY-MAP.md` | Literature rename map only; not a claim that stages are load-bearing |
| Whitepaper Draft 4/5 `.docx` | **Historical only.** Still contain overclaims. **Not rewritten by agents.** Do not hand out as architecture until Dave cuts them by hand or explicitly authorizes a cut of those files. |
| `docs/CFO-DEMO-KIT.md` / marketing | Partial soft-edit; re-check before external use |
| `scripts/adversarial_defense/` | Experiment harness only; not product |

---

## Session rule for agents

Before arguing about an “operator,” open this file and the code path.  
If it is not in the “What is real today” table as load-bearing, it is a **proposal**, not a system description.
