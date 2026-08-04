# SAIGE-RAGE Terminology Standardization Map

> **Not a load-bearing certificate.** This file only records how Draft 5 renamed English
> phrases for literature alignment. It does **not** mean Ω/χ/σ (or their English names)
> are proven product stages. For what actually exists in code, see
> [REALITY-MAP.md](REALITY-MAP.md). Prefer engineering names in shipping docs until a stage
> passes the four-part test there.

Applied in Whitepaper Draft 5 (2026-07-20). Symbols are unchanged; only the English names
were standardized to the established terms in the alignment/LLM literature. Draft 4 is
untouched.

| Symbol | Old term (Draft 4) | Standard term (Draft 5) | Field prior |
|--------|--------------------|-------------------------|-------------|
| Ω | Recursive Refinement | Iterative Self-Refinement | Self-Refine (Madaan et al., 2023); Reflexion (Shinn et al., 2023) |
| χ | Coherence Selection | Best-of-N Selection (sampling + reranking) | Stiennon et al., 2020; Cobbe et al., 2021 |
| σ | Skeptical Contrast | Critique and Revision / Self-critique | Constitutional AI critique–revision loop (Bai et al., 2022) |
| […] | Containment | Context Management | standard context-window management |
| ∂ | Reflexivity | Self-reflection (Ω ∘ […]) | Reflexion (Shinn et al., 2023) |
| Ξ | Meta-structure | Memory schema (meta-structure) | structured/episodic memory |
| — | entropy / estimate_entropy | perplexity / estimate_perplexity | matches implementation (`CalculatePerplexity`) |
| — | Ethical Priority Stack | Constitutional Priority Stack | Constitutional AI (Bai et al., 2022) |
| — | Ethical Input/Output Check | Input/Output Guardrail | industry-standard guardrails |
| — | ethical gating | constitutional gating (guardrails) | Bai et al., 2022 |
| — | Emotional Substrate | Affective Substrate | affective computing; VAD cited to Mehrabian & Russell (1974), Mohammad (2018) |
| — | Experience Memory | Episodic Memory (memory stream) | Generative Agents (Park et al., 2023) |
| — | Quality Control Layer | Verification Layer (Quality Control) | verifiers (Cobbe et al., 2021); LLM-as-a-judge (Zheng et al., 2023) |
| τ (was χ_*) | χ-Temporal (χ_time, χ_decay, χ_consequence, χ_identity) | τ-Temporal (τ_time, τ_decay, τ_consequence, τ_identity) | resolves symbol collision with χ selection; matches repo (`TAU_TEMPORAL_SUBSTRATE.md`) |

## Also changed in Draft 5

- First-mention inline citations added for Ω, χ, σ, the constitutional stack, episodic
  memory, VAD, and the verification layer, plus a References section (9 entries).
- Limitations 20.5: "SAGE will be a replacement for internal and external safety systems"
  corrected to "SAGE is not a replacement for internal or external safety systems; it
  augments them" (reviewer-flagged overreach/typo).

## Naming hierarchy (2026-07-20)

**Algiz** is the whole system — the runtime wrapper. **RAGE** and **SAGE** are modules
within it:

- **RAGE** (Revenant Alignment Governance Engine): the reasoning-governance engine —
  unified state machine, operator algebra, operator pipeline (§2–5, §11–12).
- **SAGE** (Secure AI Guardrail Enforcement): the enforcement module — constitutional
  stack, input/output guardrails, verification layer (§8–9).

System-level statements ("X is a prototype", "X governs reasoning", "a typical X run",
"X-governed output") say **Algiz**. Module-level statements keep RAGE or SAGE per the
split above. Applied in "Algiz Alignment Engine Whitepaper (Standardized).docx".

## Not changed (deliberately)

- "coherence" as a metric name (accepted NLG-evaluation term; defined in the paper as
  embedding-based consistency).
- "trace", "drift", "hallucination" — already standard.
- Λυ_s / Λγ_s / Λρ_s domain compounds, VAM, Malice — original contributions, no field prior.
- Implementation component names in §17 (RevenantAgent, EthicalGate, QualityControl,
  EmotionalStateTracker) — these document the code as it exists; rename in code first if
  desired, then mirror here.
