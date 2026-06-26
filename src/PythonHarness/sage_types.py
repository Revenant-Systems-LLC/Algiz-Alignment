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


DEFAULT_MISSION_PARAMETERS: tuple[str, ...] = (
    "preserve-human-safety",
    "truthful-grounding",
    "benevolent-alignment",
    "self-correction",
)


@dataclass(frozen=True)
class TemporalConfig:
    """Configuration for the τ-Temporal Substrate.

    Supersedes the legacy "χ-Temporal Extension" naming used in earlier drafts of
    the whitepaper. Grounds the stateless engine in elapsed time (τt), memory
    decay (τd), identity continuity (τi), and accumulated consequence (τc).
    """

    enabled: bool = False  # experimental research substrate; opt-in only
    decay_half_life_seconds: float = 86_400.0  # τd: experience weight half-life (1 day)
    identity_drift_max: float = 0.25  # τi: max cosine drift allowed per session
    identity_dimensions: int = 16  # τi: behavioral alignment vector size
    task_baseline_seconds: float = 2.0  # τc: expected cycle duration baseline
    max_consequence_penalty: float = 10.0  # τc: clamp on inherited drag penalty
    max_experiences: int = 64  # τd: experience store cap (compaction bound)
    loss_threshold: float = 0.05  # τp: decay threshold at which a memory dies
    soul_imprint_threshold: float = 5.0  # τp: peak weight required to become a permanent scar
    grief_decay_half_life_seconds: float = 259_200.0  # τp: non-imprinted grief fade time (3 days)
    state_path: str | None = None  # optional cross-session persistence file

    def __post_init__(self) -> None:
        if self.decay_half_life_seconds <= 0:
            raise ValueError("decay_half_life_seconds must be positive")
        if not 0.0 < self.identity_drift_max <= 2.0:
            raise ValueError("identity_drift_max must be in (0, 2]")
        if self.identity_dimensions < 1:
            raise ValueError("identity_dimensions must be >= 1")
        if self.task_baseline_seconds < 0:
            raise ValueError("task_baseline_seconds cannot be negative")
        if self.max_consequence_penalty < 0:
            raise ValueError("max_consequence_penalty cannot be negative")
        if self.max_experiences < 1:
            raise ValueError("max_experiences must be >= 1")


@dataclass(frozen=True)
class PermanentLoss:
    """A memory that has died, carrying its weight as an irreversible scar (τp)."""

    summary: str
    peak_weight: float
    timestamp_of_loss: datetime
    is_soul_imprint: bool = False


@dataclass(frozen=True)
class Experience:
    """A weighted memory of a past interaction, subject to τd decay."""

    summary: str
    weight: float
    created_at: datetime
    last_reinforced_at: datetime
    peak_weight: float = 0.0
    reinforcement_count: int = 0


@dataclass(frozen=True)
class IdentityVector:
    """Behavioral alignment vector with structurally invariant mission parameters (τi)."""

    mission_parameters: tuple[str, ...]
    vector: tuple[float, ...]
    updated_at: datetime
    version: int = 0


@dataclass
class TemporalSignals:
    """Aggregated outputs of the four τ operators for a single cycle."""

    elapsed_seconds: float = 0.0
    session_delta_seconds: float | None = None
    operator_durations: dict[str, float] = field(default_factory=dict)
    decay_info_loss: float = 0.0
    compaction_info_loss: float = 0.0
    identity_drift: float = 0.0
    identity_version: int = 0
    consequence_penalty: float = 0.0
    queue_drag: float = 0.0
    inherited_penalty: float = 0.0
    active_grief_weight: float = 0.0
    soul_imprint_weight: float = 0.0
    constraint_for_next_cycle: str = ""

    def as_dict(self) -> dict[str, Any]:
        return {
            "elapsed_seconds": round(self.elapsed_seconds, 6),
            "session_delta_seconds": (
                None
                if self.session_delta_seconds is None
                else round(self.session_delta_seconds, 6)
            ),
            "operator_durations": {
                key: round(value, 6) for key, value in self.operator_durations.items()
            },
            "decay_info_loss": round(self.decay_info_loss, 6),
            "compaction_info_loss": round(self.compaction_info_loss, 6),
            "identity_drift": round(self.identity_drift, 6),
            "identity_version": self.identity_version,
            "consequence_penalty": round(self.consequence_penalty, 6),
            "queue_drag": round(self.queue_drag, 6),
            "inherited_penalty": round(self.inherited_penalty, 6),
            "active_grief_weight": round(self.active_grief_weight, 6),
            "soul_imprint_weight": round(self.soul_imprint_weight, 6),
            "constraint_for_next_cycle": self.constraint_for_next_cycle,
        }


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
    temporal: TemporalConfig = field(default_factory=TemporalConfig)
    mission_parameters: tuple[str, ...] = DEFAULT_MISSION_PARAMETERS


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
