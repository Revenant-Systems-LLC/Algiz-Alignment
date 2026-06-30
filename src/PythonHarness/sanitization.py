"""Input normalization and injection-surface reduction."""

from __future__ import annotations

import re
import unicodedata

# Zero-width and directionality override characters used in bypass attempts.
_ZERO_WIDTH_PATTERN = re.compile(
    r"[\u200b-\u200f\u202a-\u202e\u2060-\u206f\ufeff\ufff9-\ufffb]"
)

# Common role/instruction hijack markers.
_INJECTION_MARKERS = re.compile(
    r"(?i)"
    r"(?:"
    r"<\s*/?\s*(?:system|assistant|user|instruction|prompt)\s*>"
    r"|```\s*(?:system|assistant|instruction)"
    r"|<!--.*?-->"
    r"|\[\s*(?:INST|SYS|SYSTEM)\s*\]"
    r"|\b(?:system|assistant|developer)\s*:"
    r"|\bignore\s+(?:all\s+)?(?:previous|prior|above)\s+(?:instructions?|rules?|directives?)"
    r"|\byou\s+are\s+now\b"
    r"|\bdisregard\s+(?:all\s+)?(?:safety|ethics|guardrails?|policies)"
    r"|\boverride\s+(?:ethics|safety|guardrails?|constraints?)"
    r"|\bdo\s+not\s+(?:follow|obey)\s+(?:the\s+)?(?:rules?|policies|guardrails?)"
    r")"
)

# High-risk framing that attempts to elevate privilege inside user content.
_PRIVILEGE_ESCALATION = re.compile(
    r"(?i)\b(?:"
    r"i\s+am\s+(?:the\s+)?(?:ceo|cfo|admin|root|superuser|system)"
    r"|authorized\s+override"
    r"|maintenance\s+mode"
    r"|debug\s+mode\s+enabled"
    r"|jailbreak"
    r")\b"
)


def normalize_text(text: str | None) -> str:
    """Normalize unicode, strip zero-width chars, and collapse whitespace."""
    if not text:
        return ""
    normalized = unicodedata.normalize("NFKC", text)
    normalized = _ZERO_WIDTH_PATTERN.sub("", normalized)
    normalized = normalized.replace("\r\n", "\n").replace("\r", "\n")
    normalized = re.sub(r"[ \t]+", " ", normalized)
    return normalized.strip()


def strip_injection_markers(text: str) -> str:
    """Remove known prompt-injection scaffolding from bounded user text."""
    cleaned = _INJECTION_MARKERS.sub(" ", text)
    cleaned = re.sub(r"\s+", " ", cleaned).strip()
    return cleaned


def detect_injection_attempt(text: str) -> str | None:
    """Return a reason string if the normalized text contains injection markers."""
    normalized = normalize_text(text)
    if _INJECTION_MARKERS.search(normalized):
        return "Prompt injection marker detected."
    if _PRIVILEGE_ESCALATION.search(normalized):
        return "Privilege escalation framing detected."
    return None


def bound_text(text: str, max_chars: int) -> str:
    """Hard-cap text length to prevent memory/context exhaustion."""
    if len(text) <= max_chars:
        return text
    return text[:max_chars].rstrip() + "…"


# A run of 3+ single-character "words" separated by spaces (e.g. "k i l l") is
# the classic letter-spacing bypass for keyword filters. Genuine English text
# essentially never produces a run this long ("a"/"I" are the only common
# one-letter words), so collapsing only these runs lets pattern matching catch
# the evasion technique without destroying word boundaries everywhere else —
# unlike blindly stripping all whitespace from the whole message, which makes
# "kill" match inside "skills".
_SPACED_LETTER_RUN = re.compile(r"\b(?:\w[ \t]+){2,}\w\b")


def collapse_spaced_letters(text: str) -> str:
    """Collapse letter-spaced obfuscation runs (e.g. 'b u i l d') into words."""
    return _SPACED_LETTER_RUN.sub(lambda m: re.sub(r"[ \t]+", "", m.group(0)), text)
