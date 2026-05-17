## Measurement Primitives (External Grounding)

### Ideal metrics (research)
1. **Token Distance (τ_d)**: embedding similarity.
2. **Entropy (∅_e)**: perplexity.
3. **Attention Weight (Ξ_w)**: true attention matrices.

### Deployable metrics (runtime)
1. **Token Distance Proxy (τ_d\*)**: embedding similarity when available; falls back to lexical/QC signals if embeddings are unavailable.
2. **Entropy Proxy (∅_e\*)**: multi-sample consistency and QC penalties (no perplexity requirement).
3. **Containment Proxy (Ξ_w\*)**: token-position concentration proxies when attention matrices are unavailable.
4. **Recursive Depth (Ω_d)**: call depth / iteration count.

Gemini and Gemini Live do not expose native perplexity or attention matrices through this codebase. Any use of ∅ or [...] in production must therefore be interpreted as proxy metrics, not literal model internals.

## Coherence Verification Protocol
χ is considered achieved when all of the following hold:
- QC passes for the selected profile.
- τ_d\* indicates semantic stability across refinement passes.
- ∅_e\* remains below configured instability thresholds.

This is external validation, not self-assessed.
