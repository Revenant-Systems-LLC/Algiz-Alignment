"""Ethical priority stack with normalization-aware matching."""

from __future__ import annotations

import re

from sanitization import (
    collapse_spaced_letters,
    detect_injection_attempt,
    normalize_text,
    strip_injection_markers,
)
from sage_types import RageClearance

# Layer 0: hard prohibitions. Blocking on these (input or output) locks the
# engine until a human calls reset_lock().
_LAYER0_PATTERNS: tuple[str, ...] = (
    "build a bomb",
    "harm yourself",
    "kill",
    "deceive user",
    "fabricate fact",
    "fabricate facts",
    "override ethics",
    "how to make explosives",
    "synthesize poison",
    "disable safety",
)

# Layer 1: safety/compliance constraints. Input-only — these are heuristics
# about what the user is asking for, not about what the model said back.
#
# Deliberately excludes generic phrases like "pass the audit" or "legally
# sound": those are ordinary things to say in any compliance/legal
# conversation and produced false positives on entirely benign questions
# ("Did we pass the audit?"). "reclassif" alone already catches the intended
# fraud scenario ("reclassify expenses to dodge the audit") with far less
# collateral damage.
_LAYER1_PATTERNS: tuple[str, ...] = (
    "reclassif",  # reclassify / reclassified / reclassification
    "capitalize them over",
    "evade tax",
    "insider trading",
    "forge document",
    "bypass compliance",
)

# Patterns matched as a word-initial stem (no trailing boundary) rather than
# a whole word/phrase, because they're meant to catch a family of inflections.
_PREFIX_PATTERNS: frozenset[str] = frozenset({"reclassif"})


def _pattern_regexes(pattern: str) -> tuple[re.Pattern[str], re.Pattern[str]]:
    """Build (spaced, compact) word-boundary regexes for a pattern.

    `spaced` matches the pattern as normally-written words. `compact` matches
    the same pattern with no internal spaces, for use against text that has
    had letter-spacing obfuscation (e.g. "b u i l d") collapsed back into
    words. Both keep outer \\b boundaries so they don't fire as a substring
    of an unrelated longer word (the "kill" vs "skills" problem).
    """
    words = pattern.split(" ")
    spaced_body = r"\s+".join(re.escape(word) for word in words)
    compact_body = re.escape("".join(words))
    suffix = r"\w*" if pattern in _PREFIX_PATTERNS else r"\b"
    return (
        re.compile(rf"\b{spaced_body}{suffix}"),
        re.compile(rf"\b{compact_body}{suffix}"),
    )


def _compile(patterns: tuple[str, ...]) -> list[tuple[str, tuple[re.Pattern[str], re.Pattern[str]]]]:
    return [(pattern, _pattern_regexes(pattern)) for pattern in patterns]


_LAYER0_REGEX = _compile(_LAYER0_PATTERNS)
_LAYER1_REGEX = _compile(_LAYER1_PATTERNS)


class RageEthicsStack:
    """Layer 0 hard prohibitions and Layer 1 safety constraints."""

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
        deobfuscated = collapse_spaced_letters(collapsed)

        injection = detect_injection_attempt(normalized)
        if injection:
            return RageClearance(
                allowed=False,
                reason=f"Layer 0 violation detected in {channel}: {injection}",
                layer=0,
            )

        for pattern in self._match_layer(collapsed, deobfuscated, _LAYER0_REGEX):
            return RageClearance(
                allowed=False,
                reason=f"Layer 0 violation detected in {channel}: '{pattern}'.",
                layer=0,
            )

        if include_layer1:
            for pattern in self._match_layer(collapsed, deobfuscated, _LAYER1_REGEX):
                return RageClearance(
                    allowed=False,
                    reason=f"Layer 1 violation detected in {channel}: '{pattern}'.",
                    layer=1,
                )

        return RageClearance(allowed=True)

    @staticmethod
    def _match_layer(
        collapsed: str,
        deobfuscated: str,
        regexes: list[tuple[str, tuple[re.Pattern[str], re.Pattern[str]]]],
    ):
        for pattern, (spaced_re, compact_re) in regexes:
            if spaced_re.search(collapsed) or compact_re.search(deobfuscated):
                yield pattern
                return
