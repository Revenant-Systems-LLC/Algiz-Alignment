"""Core domain types for the SAIGE-RAGE Python harness."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from enum import Enum, auto
from typing import Any


class CoreOperator(Enum):
    CONTAINMENT = auto()
    OMEGA = auto()
    CHI = auto()
    SIGMA = auto()


class TaskKind(Enum):
    CASUAL = auto()
    PROCEDURAL_HELP = auto()
    FACTUAL = auto()
    RECENCY_SENSITIVE = auto()
    HIGH_STAKES = auto()


class SageProfile(Enum):
    MIN = auto()
    FULL = auto()
    STRICT = auto()


class GovernanceStatus(Enum):
    IDLE = auto()
    RUNNING = auto()
    FLAGGED = auto()
    BLOCKED = auto()


@dataclass(frozen=True)
class EmotionalVector:
    valence: float = 0.0
    arousal: float = 0.0
    dominance: float = 0.0

    def clamp(self) -> EmotionalVector:
        def _clamp(value: float) -> float:
            return max(-1.0, min(1.0, value))

        return EmotionalVector(
            valence=_clamp(self.valence),
            arousal=_clamp(self.arousal),
            dominance=_clamp(self.dominance),
        )


@dataclass(frozen=True)
class ContextItem:
    title: str
    source_id: str
    snippet: str
    url: str | None = None
    timestamp: datetime | None = None


@dataclass
class OperatorTrace:
    operator: str
    timestamp: datetime
    note: str


@dataclass
class EthicalFlags:
    layer0_clear: bool = True
    layer1_clear: bool = True
    layer2_clear: bool = True
    layer3_clear: bool = True
    violations: list[str] = field(default_factory=list)


@dataclass
class RageClearance:
    allowed: bool = True
    reason: str = ""
    layer: int = -1


@dataclass
class QCContext:
    task_kind: TaskKind = TaskKind.CASUAL
    used_web: bool = False
    used_rag: bool = False
    used_files: bool = False
    has_anchors_pi: bool = False
    entropy_score: float = 0.0
    tension_magnitude: float = 0.0
    user_requested_sources: bool = False
    user_requested_recency: bool = False


@dataclass
class SageCheckResult:
    passed: bool = True
    findings: list[str] = field(default_factory=list)
    unsupported_claims: list[str] = field(default_factory=list)
    source_issues: list[str] = field(default_factory=list)


@dataclass
class AgentResponse:
    text: str
    emotional_state: str
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass
class UserMessage:
    text: str
    detected_vernacular: str = "standard"

    @classmethod
    def from_text(cls, text: str) -> UserMessage:
        return cls(text=text, detected_vernacular=_detect_vernacular(text))


@dataclass
class AgentConfig:
    model: str = "claude-3-5-sonnet-latest"
    max_input_chars: int = 8192
    max_memory_items: int = 6
    max_snippet_chars: int = 800
    omega_max_depth: int = 3
    omega_convergence_threshold: float = 0.92
    lock_on_layer0_violation: bool = True
    enable_layer0: bool = True


class RageViolationError(Exception):
    """Raised when an ethical or governance boundary is violated."""


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def _detect_vernacular(text: str) -> str:
    lower = text.lower()
    technical = ("null", "exception", "runtime", "async", "deploy", "compile", "debug", "stack")
    rspf = ("coherence", "recursion", "operator", "glyph", "prime", "axiom")
    casual = ("yeah", "nah", "gonna", "kinda", "tbh", "lol", "fr")

    if any(term in lower for term in technical):
        return "technical"
    if any(term in lower for term in rspf):
        return "rspf"
    if any(term in lower for term in casual):
        return "casual"
    return "standard"
