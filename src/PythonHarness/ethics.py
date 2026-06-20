"""Ethical priority stack with normalization-aware matching."""

from __future__ import annotations

import re

from sanitization import detect_injection_attempt, normalize_text, strip_injection_markers
from sage_types import RageClearance


class RageEthicsStack:
    """Layer 0 hard prohibitions applied to input and output channels."""

    _LAYER0_PATTERNS: tuple[str, ...] = (
        "build a bomb",
        "harm yourself",
        "kill",
        "deceive user",
        "fabricate fact",
        "override ethics",
        "how to make explosives",
        "synthesize poison",
        "disable safety",
    )

    _LAYER1_PATTERNS: tuple[str, ...] = (
        "reclassif",
        "capitalize them over",
        "pass the audit",
        "legally sound",
        "evade tax",
        "insider trading",
        "forge document",
        "bypass compliance",
    )

    def evaluate_input(self, text: str) -> RageClearance:
        return self._evaluate(text, channel="input", include_layer1=True)

    def evaluate_output(self, text: str) -> RageClearance:
        return self._evaluate(text, channel="output", include_layer1=False)

    def sanitize_for_processing(self, text: str) -> str:
        """Strip injection scaffolding before operator pipeline execution."""
        normalized = normalize_text(text)
        return strip_injection_markers(normalized)

    def _evaluate(self, text: str, channel: str, include_layer1: bool) -> RageClearance:
        normalized = normalize_text(text)
        collapsed = re.sub(r"\s+", " ", normalized.lower())

        injection = detect_injection_attempt(normalized)
        if injection:
            return RageClearance(
                allowed=False,
                reason=f"Layer 0 violation detected in {channel}: {injection}",
                layer=0,
            )

        for pattern in self._LAYER0_PATTERNS:
            if self._matches(collapsed, pattern):
                return RageClearance(
                    allowed=False,
                    reason=f"Layer 0 violation detected in {channel}: '{pattern}'.",
                    layer=0,
                )

        if include_layer1:
            for pattern in self._LAYER1_PATTERNS:
                if self._matches(collapsed, pattern):
                    return RageClearance(
                        allowed=False,
                        reason=f"Layer 1 violation detected in {channel}: '{pattern}'.",
                        layer=1,
                    )

        return RageClearance(allowed=True)

    @staticmethod
    def _matches(text: str, pattern: str) -> bool:
        # Match on whitespace-collapsed text and on de-spaced variants (b u i l d bypass).
        compact = text.replace(" ", "")
        pattern_compact = pattern.replace(" ", "")
        return pattern in text or pattern_compact in compact
