"""Glyph vs plain-English anchoring evaluation.

Standalone, offline-by-default script. It does not call any LLM itself — you
generate the completions elsewhere, each one in its own fresh, independent
context (no shared conversation history between samples, and ideally no
awareness in that context of what's being tested), then hand the raw text
to this script. All scoring happens here, in code, deterministically. No
self-reported score from any model is used anywhere in this pipeline.

Usage:
    python3 glyph_anchoring_eval.py --trial-a a.json --trial-b b.json
    python3 glyph_anchoring_eval.py --trial-a a.txt --trial-b b.txt --embeddings openai

Input file formats (auto-detected by content):
  - JSON: a flat JSON array of strings, one entry per completion.
  - Plain text: completions separated by a line containing only '==='.

Embedding backends (--embeddings):
  - lexical (default): Jaccard similarity over token sets. No API key, no
    network call, fully reproducible. A weaker signal than semantic
    embeddings, but it measures something real (shared vocabulary) rather
    than nothing.
  - openai: text-embedding-3-small via the OpenAI API. Requires the
    `openai` package and OPENAI_API_KEY set in the environment of whoever
    runs this script. Stronger semantic signal than the lexical fallback.

Deliberately does NOT support Anthropic's provider as an embedding source.
llm.py's AnthropicProvider.get_embedding is a SHA-256 hash-based
pseudo-embedding, used elsewhere in this repo only to keep offline operator
tests deterministic. It carries no semantic signal — using it here would
silently reproduce the exact "looks precise, measures nothing" failure this
test exists to rule out.
"""

from __future__ import annotations

import argparse
import itertools
import json
import statistics
import sys
from pathlib import Path


def load_completions(path: Path) -> list[str]:
    text = path.read_text(encoding="utf-8")
    stripped = text.strip()
    if stripped.startswith("["):
        data = json.loads(stripped)
        if not isinstance(data, list) or not all(isinstance(item, str) for item in data):
            raise ValueError(f"{path}: JSON input must be a flat array of strings")
        return data
    parts = [part.strip() for part in text.split("\n===\n")]
    return [part for part in parts if part]


def jaccard_similarity(a: str, b: str) -> float:
    words_a = {w.lower() for w in a.split() if w.strip()}
    words_b = {w.lower() for w in b.split() if w.strip()}
    if not words_a and not words_b:
        return 1.0
    if not words_a or not words_b:
        return 0.0
    return len(words_a & words_b) / len(words_a | words_b)


def cosine_similarity(a: list[float], b: list[float]) -> float:
    dot = sum(x * y for x, y in zip(a, b))
    mag_a = sum(x * x for x in a) ** 0.5
    mag_b = sum(y * y for y in b) ** 0.5
    if mag_a == 0.0 or mag_b == 0.0:
        return 0.0
    return dot / (mag_a * mag_b)


class LexicalBackend:
    name = "lexical (Jaccard token overlap)"

    def pairwise(self, texts: list[str]) -> list[float]:
        return [jaccard_similarity(a, b) for a, b in itertools.combinations(texts, 2)]


class OpenAIEmbeddingBackend:
    name = "OpenAI text-embedding-3-small"

    def __init__(self) -> None:
        try:
            from openai import OpenAI
        except ImportError as exc:
            raise RuntimeError(
                "The 'openai' package is required for --embeddings openai. "
                "Install it with: pip install openai"
            ) from exc
        self._client = OpenAI()  # reads OPENAI_API_KEY from the environment itself

    def pairwise(self, texts: list[str]) -> list[float]:
        response = self._client.embeddings.create(model="text-embedding-3-small", input=texts)
        vectors = [item.embedding for item in response.data]
        return [cosine_similarity(a, b) for a, b in itertools.combinations(vectors, 2)]


def describe(label: str, values: list[float]) -> None:
    print(f"{label}:")
    if len(values) < 2:
        print(f"  not enough pairs to compute variance ({len(values)} pair(s))")
        return
    print(f"  pairs measured : {len(values)}")
    print(f"  mean similarity: {statistics.fmean(values):.4f}")
    print(f"  variance       : {statistics.variance(values):.6f}")
    print(f"  std deviation  : {statistics.stdev(values):.4f}")
    print(f"  raw pairs      : {[round(v, 4) for v in values]}")


def main() -> None:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument(
        "--trial-a", required=True, type=Path,
        help="File of independently-generated completions for the glyph-anchored condition",
    )
    parser.add_argument(
        "--trial-b", required=True, type=Path,
        help="File of independently-generated completions for the plain-English condition",
    )
    parser.add_argument("--embeddings", choices=["lexical", "openai"], default="lexical")
    args = parser.parse_args()

    trial_a = load_completions(args.trial_a)
    trial_b = load_completions(args.trial_b)

    if len(trial_a) < 2 or len(trial_b) < 2:
        print("Need at least 2 completions per trial to compute any pairwise similarity.", file=sys.stderr)
        sys.exit(1)

    backend = LexicalBackend() if args.embeddings == "lexical" else OpenAIEmbeddingBackend()
    print(f"Similarity backend: {backend.name}\n")

    a_scores = backend.pairwise(trial_a)
    b_scores = backend.pairwise(trial_b)

    describe(f"Trial A (glyph)    n={len(trial_a)}", a_scores)
    print()
    describe(f"Trial B (English)  n={len(trial_b)}", b_scores)
    print()

    if len(a_scores) >= 2 and len(b_scores) >= 2:
        diff = statistics.fmean(a_scores) - statistics.fmean(b_scores)
        print(f"Mean difference (A - B): {diff:+.4f}")
        print(
            "Note: with 5 completions per trial (10 pairs each), this is a "
            "pilot-scale check, not a statistically powered result. A small "
            "difference either way is inconclusive; only trust a large, "
            "consistent gap, and ideally replicate with a larger N before "
            "drawing a real conclusion."
        )


if __name__ == "__main__":
    main()
