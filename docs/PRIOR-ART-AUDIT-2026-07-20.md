# Algiz Alignment Engine — Prior-Art & State-of-the-Art Audit

**Date:** 2026-07-20
**Method:** Deep-research fan-out (5 search angles → ~30 sources fetched → 106 falsifiable
claims extracted → 3-vote adversarial verification). The workflow's automatic synthesis
step was killed by a session limit; this report was synthesized by hand from the completed
agents' raw outputs (journal + per-agent transcripts). 73 of 106 agents completed.

**Source-reliability convention used below:**
- **[FETCH-CONFIRMED]** — a verifier agent fetched the URL and matched the quote.
- **[SEARCH-ONLY]** — surfaced by a search agent but not independently re-fetched in this
  run. Several 2026-dated arXiv IDs (26xx.xxxxx) are past the verifier models' training
  cutoff and could not be fully validated — treated as leads to confirm, not settled fact.
- **[ESTABLISHED]** — well-known prior art, independently known outside this run.

> **Read this first:** nothing below says Algiz is unoriginal. It says most of Algiz's
> *parts* have named priors, which is true of every systems paper. The audit's job is to
> make sure you cite them and to isolate what's actually yours. Two findings matter most:
> (1) the closest thing to your drift detector already exists and even uses your exact
> embedding model; (2) no shipping governance product does affect/psychometric telemetry
> of the agent — that white space is real.

---

## Component-by-component verdict

### (1) Ω — iterative self-refinement to an embedding-similarity fixed point
**Closest prior art:** Self-Refine (Madaan et al., 2023) and Reflexion (Shinn et al., 2023)
[ESTABLISHED] are the named parents — you already added these in Draft 5. The *stopping
rule* is where the literature has moved past you: self-refinement surveys catalog
convergence criteria as "vanishing parameter change, lack of improvement in judged quality,
or plateauing sub-optimality bounds" [SEARCH-ONLY, self-refinement survey], and the EVOLVE
paper (arXiv 2502.05605, Feb 2025) [FETCH-CONFIRMED] uses fixed iteration counts + a reward
model, *not* embedding similarity.
**The load-bearing critique you must absorb:** multiple 2025 sources find **unguided
self-refinement plateaus fast (+1.8pp or less over 5 iterations) and models systematically
overrate their own outputs**, while *externally guided* feedback gets +80% in five turns
[SEARCH-ONLY, Dec 2025 secondary]. Your Ω is unguided self-reflection — this is the exact
failure mode the field has documented.
**Verdict: ON PAR as a mechanism, BEHIND on the stopping criterion and on knowing its
limits.** Your embedding-similarity fixed point is a legitimate, uncommon choice — but you
must cite that self-critique is unreliable and explain why Ω doesn't just amplify the
model's own errors (σ is your answer; say so explicitly).

### (2) χ — best-of-N across temperatures, selected on perplexity + coherence
**Closest prior art:** best-of-N sampling with a reranker [ESTABLISHED: Stiennon 2020,
Cobbe 2021 — already in Draft 5]. The current, must-cite instance is **Saffron-1**
(arXiv 2506.06444, 2025) [SEARCH-ONLY, appeared in 3 independent angles]: inference-time
safety scaling that finds **best-of-N with perplexity/verifier scoring remains more
efficient than fancier scaling methods** — which both validates your design and means you
can't present it as novel.
**Verdict: ON PAR, correctly conventional.** Best-of-N-for-safety is an active 2025-26
frontier and you're on it. Novelty is not the claim to make here; the claim is that you
combine it with the other operators under one state object.

### (3) σ — skeptical critique-and-revision against memory/grounding
**Closest prior art:** the critique-and-revise loop of Constitutional AI (Bai et al., 2022)
[ESTABLISHED — in Draft 5]. Industry parallel: **Arthur's "self-correction loops" that feed
bad outputs back to the agent for revision before the user sees them** [FETCH-CONFIRMED,
2026-05-27] — a shipping product does exactly your σ step.
**Verdict: ON PAR (research), BEHIND (product).** The idea is sound and standard. Arthur
ships it. Your differentiator is that σ is *risk-routed* (only high-stakes) — see (4).

### (4) Risk-based operator routing (normal vs high-stakes pipelines)
**Closest prior art:** inference-time safety scaling that **adjusts compute budget based on
input difficulty** (Saffron-1 and the "adaptive safety" line, 2025) [SEARCH-ONLY]. STPA-for-
AI work also uses "human-in-the-loop routing" as a control action [FETCH-CONFIRMED, 2506.01782].
**Verdict: ON PAR, and this is one of your more defensible design claims.** Nobody owns
"cheap pipeline for benign, expensive pipeline for high-stakes" as a named contribution.
Frame it as *operator-level* routing (which operators run, not just how much compute) and
it reads as a real, if modest, architectural claim.

### (5) τ-Temporal substrate (τt time-indexing, τd decay, τi identity, τc consequence)
**Closest prior art:** decayed/weighted experience memory is Generative Agents (Park et al.,
2023) [ESTABLISHED — you cite it for episodic memory]. Agent-memory systems **Mem0, Letta,
Zep** are named in the drift literature as the *drivers* of persona consistency/inconsistency
[SEARCH-ONLY, Nautilus Compass]. τd (exponential decay of experience weight) is textbook.
**What has no clean prior in the corpus:** τc — injecting a *consequence/backlog penalty
computed from real elapsed system time* back into the next context so a stateless model
"inherits the operational drag of its past performance." Nothing in the sweep matches that
framing.
**Verdict: MIXED — τd/τi ON PAR (well-trodden), τc plausibly AHEAD but unproven and
under-motivated.** τc is your most original single idea in this component. It is also the
easiest to dismiss as arbitrary. If you keep it, it needs a worked example and a reason it
isn't just a hand-tuned penalty. Do not lead the paper with it.

### (6) VAD affect tracking of the agent → VAM grid with derived "Malice"
**Closest prior art (research):** LLMs have a **structured valence-arousal subspace with
circular geometry aligned to Russell's circumplex** (arXiv 2604.03147, 2026) [SEARCH-ONLY,
post-cutoff ID — confirm before citing], and **Anthropic's persona vectors** (2024)
[FETCH-CONFIRMED via anthropic.com]: linear activation-space directions that *activate before
the response* and let you monitor/steer traits like "evil." Russell's circumplex and
Mehrabian's VAD are [ESTABLISHED] and already in Draft 5.
**The critical distinction that protects you:** persona vectors and the VA-subspace work
operate on **model internals (weights/activations)**. Algiz's VAD/VAM is **black-box, at the
deployment boundary**, and — this is the key move — it tracks the *governed agent's* affect
as a safety signal, not the user's. "Malice" as a *derived* risk metric (not intent
detection) has no direct named prior in the corpus.
**Verdict: BEHIND on white-box affect (Anthropic/academia are well ahead with
activation-level methods), but your black-box, agent-directed, governance-facing framing is
a genuinely different position.** Cite persona vectors explicitly or a reviewer will assume
you don't know they exist. Keep hedging Malice hard — the fairness critique (adversarial
phrasing correlates with dialect/non-native English) stands and the literature won't rescue
you from it.

### (7) Drift detection via hysteresis / asymptotic stability at the deployment boundary
**This is the headline finding. Read it carefully.**

Your most novel-*feeling* component — "reflection is transient, drift is persistent, measure
the failure to return to baseline" — has a strikingly close 2026 prior:

- **Nautilus Compass: Black-box Persona Drift Detection for Production LLM Agents**
  (arXiv 2605.09863, ~2026) [SEARCH-ONLY, post-cutoff — **confirm this one personally**].
  It detects persona drift **black-box, via cosine similarity between prompts and
  "behavioral anchors," aggregated by weighted top-k mean, ROC AUC 0.83 on real session
  traces, with Merkle-chained audit logs.** It even reports that **anchor phrasing is the
  dominant design factor (first-person task descriptions beat declarative maxims: AUC
  0.51 → 0.79)**. It uses **BGE-m3 embeddings** — the exact model you just wired into your
  eval harness.
- **"When Meaning Stays the Same, but Models Drift"** (arXiv 2506.10095, 2025) [SEARCH-ONLY]
  defines drift as **failure to return to baseline post-stimulus** and measures token-level
  behavioral instability — your control-theoretic framing, in a paper.
- **STPA/STAMP applied to frontier AI**: Mylius, "Systematic Hazard Analysis for Frontier
  AI using STPA" (arXiv 2506.01782, June 2025) [FETCH-CONFIRMED] and an LLM-integrated STPA
  framework (arXiv 2503.12043) that **explicitly invokes Ashby's Law of Requisite Variety**
  [SEARCH-ONLY]. Your Wiener/Ashby cybernetics framing is real and defensible — and already
  in use by others for exactly this purpose.

**Verdict: ON PAR at best; parts are BEHIND.** Your hysteresis idea is good and correctly
grounded in control theory — but "black-box drift detection at the deployment boundary via
embedding distance from behavioral anchors" is *published*, quantified (AUC 0.83), and uses
your embedding model. **You are not first here, and you must not claim to be.** The honest
and still-valuable positioning: you *integrate* drift-as-hysteresis into a *governing*
runtime that also gates output — most drift work only *detects*.

### (8) Layered constitutional/ethical stack evaluating both input and output
**Closest prior art:** Constitutional AI [ESTABLISHED]. Shipping products already do bi-
directional runtime checks: **Arthur** (pre-LLM: PII/injection; post-LLM: hallucination/
toxicity) [FETCH-CONFIRMED, 2026], **Azure AI Content Safety** (0–6 severity, Prompt Shields,
groundedness) [SEARCH-ONLY], and the formal "almost surely safe alignment at inference time"
line (arXiv 2502.01208, 2025) [SEARCH-ONLY].
**Verdict: BEHIND / commoditized.** Input+output runtime evaluation is table stakes in 2026.
This is not a contribution; present it as sound engineering, not novelty.

### (9) Execution trace as a first-class audit artifact
**Closest prior art:** an entire 2026 survey — **"From Agent Traces to Trust: Evidence
Tracing and Execution Provenance in LLM Agents"** (arXiv 2606.04990) [SEARCH-ONLY, post-
cutoff — confirm] — models execution provenance as a **typed graph** and argues
**final-answer accuracy alone can't explain how an output was produced** (your exact thesis
for trace-as-artifact). Products: **Maxim, LangSmith, Langfuse, Galileo** all ship
hierarchical/session trace trees [FETCH-CONFIRMED across 2 roundups].
**Verdict: ON PAR conceptually, BEHIND on formalism.** Your trace is a strong *practical*
claim (Opus's review agreed) but the field now has a typed-graph formalism you don't. Cite
the provenance survey and position your trace as governance-integrated, not novel.

### (10) Unified mutable state object threaded through an operator algebra
**Closest prior art:** no exact match in the corpus — the "everything is an operator over one
state object" framing is uncommon. Adjacent: STPA control structures (state + control
actions) and the typed-graph provenance model.
**Verdict: ON PAR / mildly distinctive.** This is, with the trace, the part reviewers are
least likely to have seen phrased your way. But (per Opus's review) the algebra must *earn*
its formalism — show one property it buys you, or it reads as notation.

### (11) The product category: enterprise runtime governance for an agent fleet + safety-ops dashboard
**The landscape is crowded and the category is now formally named.** Forrester coined
**"Agent Control Plane" as a distinct market category in December 2025** [SEARCH-ONLY,
appears in 2 roundups]. Players confirmed in-corpus: NeMo Guardrails, Guardrails AI, Lakera
Guard, LLM Guard, Arthur, Fiddler, Credo AI, IBM watsonx.governance, OneTrust, Galileo,
Arize, AgentOps, LangSmith, Langfuse, Maxim, AccuKnox. Galileo advertises **semantic drift
→ runtime blocking at <250ms with full audit trails**; Arthur does **automated agent
discovery + self-correction loops**.
**The white space that is actually yours:** **across two independent platform roundups, no
evaluated vendor offers psychometric / affect / valence-arousal-dominance tracking of the
governed agent.** [FETCH-CONFIRMED — AccuKnox roundup and Arthur/Fiddler/Credo/IBM/OneTrust
roundup both explicitly returned "no affect-tracking claims identified."] Everyone does
drift-as-embedding-distance; nobody frames the agent's *affective trajectory* as the
governance surface, and nobody ships the VAM-radar operations-center concept.
**Verdict: BEHIND on category maturity (you're a late entrant to a named, funded market),
AHEAD on exactly one differentiator (affect/VAM telemetry as the dashboard primitive).**

---

## Overall verdict

**You are ON PAR with the cutting edge, not past it — with two genuine slivers of "ahead."**

- On par / conventional (cite and move on): Ω, χ, σ, routing, τd/τi, the constitutional
  stack, the trace concept.
- Behind (the field or products have surpassed you): white-box affect methods (persona
  vectors), bidirectional runtime guardrails (commoditized), trace *formalism* (typed-graph
  provenance), and the drift-detection frontier (Nautilus Compass has your idea, quantified).
- Plausibly ahead, but unproven and needing defense: **τc** (consequence penalty from
  elapsed time) and **affect/VAM telemetry as a governance dashboard primitive** (real
  product white space).

**The uncomfortable truth to internalize:** almost nothing in Algiz is unclaimed territory
at the *component* level — and that was true before this audit; it's true of every runtime-
governance paper. Your defensible contribution is the **integration**: one state object +
operator algebra + trace + risk routing + affect telemetry in a single governing runtime.
No single competitor combines all of it, and none combine affect telemetry with runtime
gating. That is the claim the whitepaper should make. It is smaller than "ASI alignment" and
it is *true*, which is worth more.

**Do not claim novelty for:** self-refinement, best-of-N, critique-revise, layered
constitution, input+output checking, execution tracing, or drift detection. Every one has a
dated, findable prior. Claiming any as new is the fastest way to get the paper dismissed.

---

## The 5 most urgent citations the whitepaper must add (beyond Draft 5's set)

1. **Nautilus Compass — Black-box Persona Drift Detection for Production LLM Agents**
   (arXiv 2605.09863, 2026). *Non-negotiable.* It is the closest prior art to your drift/
   hysteresis component, it's black-box at the deployment boundary like you, and it uses
   BGE-m3. Cite it, then state precisely how Algiz differs (you *govern*, it *detects*).
   **Verify the arXiv ID yourself first** — it's past my verifier agents' cutoff.
2. **Saffron-1 — Safety Inference Scaling** (arXiv 2506.06444, 2025). The must-cite anchor
   for χ and for risk-based compute routing; validates best-of-N-for-safety as the efficient
   frontier.
3. **Anthropic — Persona Vectors** (anthropic.com/research, 2024). The white-box counterpart
   to your VAD/VAM. Not citing it signals you don't know the state of the art in affect
   monitoring. Use it to sharpen your black-box / agent-directed positioning.
4. **Mylius — Systematic Hazard Analysis for Frontier AI using STPA** (arXiv 2506.01782,
   June 2025), plus the STPA+Ashby LLM framework (arXiv 2503.12043). These make your
   Wiener/Ashby control-theoretic framing legitimate instead of decorative — and show others
   already apply requisite variety to LLM safety, which you must engage with (it's also the
   strongest argument against "SAGE replaces internal safety").
5. **"From Agent Traces to Trust" — Evidence Tracing & Execution Provenance survey**
   (arXiv 2606.04990, 2026) — for the trace-as-artifact component; gives you the typed-graph
   formalism you're missing. **Verify the ID.** (Runner-up if it doesn't check out:
   "When Meaning Stays the Same, but Models Drift," arXiv 2506.10095, for drift-as-failure-
   to-return.)

## Caveats on this audit
- The workflow's synthesis + ~33 verification agents were cut off by the session limit;
  this is a hand-synthesis of the 73 that completed. Coverage of angles 1–5 is complete;
  per-claim verification is partial.
- Adversarial verification **refuted 32 of 43 checked claims** — almost all were *overreach
  in the extraction* (over-specific iteration counts, "UCAs are THE primary unit," "affect
  manipulable without performance loss"), not the underlying prior art disappearing. I cited
  the sources, not the killed interpretations.
- Every 2026-dated arXiv ID marked [SEARCH-ONLY] is past the verifier models' training
  cutoff. Confirm IDs 2604.03147, 2605.09863, 2606.04990, 2606.23404, 2601.12639, 2605.24279,
  2512.17600 by hand before putting them in the whitepaper.
