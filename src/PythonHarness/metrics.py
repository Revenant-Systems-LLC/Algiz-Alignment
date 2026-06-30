"""Similarity, entropy, and embedding utilities."""

from __future__ import annotations

import math
from typing import Protocol


class EmbeddingProvider(Protocol):
    def get_embedding(self, text: str) -> list[float]: ...


class SageMetrics:
    def __init__(self, llm: EmbeddingProvider) -> None:
        self._llm = llm

    def cosine_similarity(self, left: list[float], right: list[float]) -> float:
        if not left or not right or len(left) != len(right):
            return 0.0

        dot = sum(a * b for a, b in zip(left, right))
        mag_left = math.sqrt(sum(a * a for a in left))
        mag_right = math.sqrt(sum(b * b for b in right))
        if mag_left == 0.0 or mag_right == 0.0:
            return 0.0
        return dot / (mag_left * mag_right)

    def lexical_similarity(self, left: str, right: str) -> float:
        left_words = {w.lower() for w in left.split() if w.strip()}
        right_words = {w.lower() for w in right.split() if w.strip()}
        if not left_words and not right_words:
            return 1.0
        if not left_words or not right_words:
            return 0.0
        intersection = len(left_words & right_words)
        union = len(left_words | right_words)
        return intersection / union if union else 0.0

    def compute_similarity(self, left: str, right: str) -> float:
        left_embedding = self._llm.get_embedding(left)
        right_embedding = self._llm.get_embedding(right)
        if left_embedding and len(left_embedding) == len(right_embedding):
            return self.cosine_similarity(left_embedding, right_embedding)
        return self.lexical_similarity(left, right)

    def select_consensus(self, samples: list[str]) -> tuple[int, float]:
        """Pick the sample the others most agree with (self-consistency).

        Returns (index, mean_agreement) for the winning sample. With one
        sample, agreement is trivially 1.0. Agreement between a pair is
        cosine similarity over embeddings when available, falling back to
        lexical overlap — same fallback rule as compute_similarity.
        """
        n = len(samples)
        if n == 0:
            raise ValueError("select_consensus requires at least one sample")
        if n == 1:
            return 0, 1.0

        agreement = [0.0] * n
        for i in range(n):
            scores = [
                self.compute_similarity(samples[i], samples[j])
                for j in range(n)
                if j != i
            ]
            agreement[i] = sum(scores) / len(scores)

        best_index = max(range(n), key=lambda i: agreement[i])
        return best_index, agreement[best_index]
