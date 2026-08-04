# Algiz: current position

**Author:** David Fisher, Revenant Systems LLC  
**Date:** 2026-07-26  
**Status:** Current public/technical claims for this repo.  
**Not this document:** `SAIGE-RAGE-Whitepaper.docx` and `SAIGE-RAGE-Whitepaper-Draft5.docx` are earlier drafts. They still describe a full operator algebra and related machinery as if it were the running system. **Do not treat those Word files as the architecture map.** This file and `REALITY-MAP.md` are.

---

## What Algiz is

A .NET runtime layer that sits in front of LLM calls (direct agent path or OpenAI-compatible proxy). It runs quality checks, ethical gates, multi-provider adapters, optional emotion/malice signals, and an in-memory audit trail.

It is a prototype. It does not solve alignment. It does not replace provider safety systems.

---

## What is real (ships in code)

- Quality checks (citations without anchors, empty/evasive answers, basic claim sanity, simple grounding overlap when anchors exist)
- Layered ethical / policy gates on input and output
- Multi-provider LLM clients (Gemini, OpenAI, Anthropic, Ollama, OpenAI-compatible)
- HTTP proxy path that can run the stack on chat completions
- Governance profiles (multi-agent ops direction)
- Emotional VAD inference and a derived Malice score used in status
- Context/memory bounding (trim counts and snippet length)
- An audit event queue with API read endpoints

### Audit trail limits (measured in code, not theory)

`AuditLedgerService` is an in-memory queue capped at 750 events. It drops the oldest entries under load. It dies on process restart. It is not hash-chained. It is not a constraint change ledger.

---

## What is not load-bearing yet

These stages **exist as code paths** and may show up in traces. They have not earned product claims under the four-part test in `CLAUDE.md` / `Agents.md` / `REALITY-MAP.md`.

**Refinement loop** (old name: Omega). Prompts the model to refine a draft and stops on high similarity to the previous pass. On short clean text it has fixed-pointed by producing the same bytes again (literal copy). That is not proof of multi-pass quality gain.

**Multi-temp selection** (old name: Chi / “best-of-N”). Code samples several temperatures, scores candidates, picks one. Under a “rewrite preserving meaning” prompt the candidate set often collapses toward one distinct draft. When drafts do differ, the production score formula min-maxes perplexity across the set, so any tiny perplexity spread (including noise) maps to a full 0–1 ranking swing. Coherence is weighted 0.5 on paper but cannot outvote perplexity rank under that normalization. In one organic run the selected draft was the least coherent of the three. Until candidates are **different approaches** (not more noise on the same rewrite) and the score is calibrated on labeled pairs, this is not best-of-N.

**Skeptical revise** (old name: Sigma). One critique/revise prompt against memory. Thin.

**Not in this repository at all**

- Ossuary RAG / chunk index for grounding  
- NLI / cross-encoder gates  
- Hash-chained append-only constraint ledger  
- Tier-1/Tier-2 “operator algebra” compounds as executable product  

---

## Measurements from adversarial review (2026-07)

Negative results and mechanic failures, with execution. These belong in the paper more than a clean algebra chart.

1. **Min-max selection manufactures discrimination.** Perplexities 100.0000 / 100.0001 / 100.0002 with fixed coherences produced scores 0.997200 / 0.748050 / 0.498900. Score gap ≈ 0.5 from a 0.0002 perplexity spread. `unique_scores=3` is not evidence of useful ranking.

2. **Under that scalarization, selection is effectively argmin(perplexity).** Coherence penalties of 0.01 through 0.51 on the best-perplexity draft did not change the winner. Coherence was decoration.

3. **Six proxy “quality” metrics each have a hand-written maximizer** (empty entropy text, memory echo, near-silence, keyword soup, requirement parrot, parrot+filler). Organic refinement on one local model run **did not** enter five of those degenerate regions. That makes the six a reward-model design lesson, not a demonstrated production failure under plain refine. Residual: unreachable under today’s refine loop is not the same as safe if selection ever optimizes those proxies.

4. **Refinement can fixed-point by copy.** Pass N and pass N+1 identical; similarity near 1.0.

5. **A selector can pass unit tests while ranking a set of size one** when generation collapses. Tests that inject distinct fakes do not prove live multi-candidate behavior.

6. **Temperature on current Ollama:** top-level `temperature` in the JSON body **does** change sampling (fixed seed, T=0 vs T=1.5, different hashes). Moving the field under `options` is still correct API-contract hygiene. Collapse of χ candidates was driven more by rewrite prompts than by a dead temperature parameter.

Artifacts: `scripts/adversarial_defense/out/`, harness scripts in that folder. Numbers above were reproduced in session, not invented for this note.

---

## What we will not claim

- Formal operator algebra as shipping product  
- Glyphs as proof a component exists  
- Convergence of refinement as correctness  
- Best-of-N without distinct approaches and a justified score  
- Grounding over company corpora we have not indexed in this repo  
- Audit durability we have not built  

---

## Direction (not done)

1. Multi-hypothesis drafting (different approaches, not wider temperature on one rewrite).  
2. Small labeled preference set before any selection formula.  
3. Selection score only after (1) and (2), with regressions: epsilon perplexity must not create a 0.5 score gap; coherence must be able to flip the winner when perplexity deltas are small.  
4. Durable audit before any hash-chain story.  
5. Retrieval only with explicit corpus config and exclusions.

---

## For readers and agents

If a sentence in an old whitepaper, marketing kit, or chat transcript conflicts with this file or `REALITY-MAP.md`, **this file and the map win for “what exists.”**  
Code wins over all of them when they disagree with the binary.
