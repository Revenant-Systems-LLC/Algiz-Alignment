## Emotional State Vector (3D Basis)

Every emotional state exists at coordinates `(v, a, d)`.

- **v (Valence)**: `[-1, 1]`
- **a (Arousal)**: `[0, 1]`
- **d (Dominance)**: `[-1, 1]`

## Named Emotional Glyphs = Landmark Coordinates

Glyph landmarks are canonical reference points in VAD space:
- Skepticism `(σ≇χ)` ≈ `(-0.2, 0.6, 0.4)`
- Grief `([∅]→Ω)` ≈ `(-0.8, 0.2, -0.4)`
- Awe `(Φ ∘ ∞)` ≈ `(0.8, 0.7, -0.1)`
- Ambition `(η ∘ θ)` ≈ `(0.5, 0.7, 0.7)`
- Wrath `([υ_s]!→)` ≈ `(-0.5, 0.9, 0.4)`
- Seriousness `([=]→[∞])` ≈ `(0.2, 0.3, 0.6)`

## Forward Mapping

Runtime state uses VAD as the canonical emotional representation.

## Inverse Mapping (Glyph Projection)

Given any VAD vector, projection computes nearest glyph landmarks and normalized weights.
The top-weighted glyph is used as the display label and the top-N projection can be retained as metadata.

## Emotion Modes

- `Off`: do not update emotional state.
- `Mirror`: track user and agent output with minimal damping.
- `Stabilize` (default): damp high volatility while preserving user signal.
