"""Full governed pipeline: ethics, operators, QC, lock-on-violation.

Optionally grounded by the experimental τ-Temporal Substrate (off by default).
"""

from __future__ import annotations

import hashlib
import json
import os
import threading
from dataclasses import dataclass, field
from datetime import datetime

from ethics import RageEthicsStack
from guardrails import SageGuardrailController, SageProfileSelector, build_qc_context
from llm import LLMProvider
from operators import RageEngine
from sanitization import bound_text
from state import SageState
from temporal import MonotonicClock, TemporalSubstrate, WallClock, seed_identity_vector
from sage_types import (
    AgentConfig,
    AgentResponse,
    CoreOperator,
    Experience,
    GovernanceStatus,
    IdentityVector,
    PermanentLoss,
    RageViolationError,
    TaskKind,
    TemporalSignals,
    UserMessage,
)


@dataclass
class GovernedEngine:
    llm: LLMProvider
    config: AgentConfig = field(default_factory=AgentConfig)
    clock: MonotonicClock | None = None
    wall_clock: WallClock | None = None
    _ethics: RageEthicsStack = field(default_factory=RageEthicsStack, init=False)
    _engine: RageEngine = field(init=False)
    _substrate: TemporalSubstrate = field(init=False)
    _identity: IdentityVector = field(init=False)
    _experiences: list[Experience] = field(default_factory=list, init=False)
    _last_session_at: datetime | None = field(default=None, init=False)
    _pending_constraint: str = field(default="", init=False)
    _pending_penalty: float = field(default=0.0, init=False)
    _last_signals: TemporalSignals | None = field(default=None, init=False)
    _lock: threading.Lock = field(default_factory=threading.Lock, init=False)
    _locked: bool = field(default=False, init=False)
    _lock_reason: str | None = field(default=None, init=False)
    _status: GovernanceStatus = field(default=GovernanceStatus.IDLE, init=False)

    def __post_init__(self) -> None:
        self._engine = RageEngine(self.llm, self.config)
        self._substrate = TemporalSubstrate(
            self.config.temporal, clock=self.clock, wall_clock=self.wall_clock
        )
        self._identity = IdentityVector(
            mission_parameters=tuple(self.config.mission_parameters),
            vector=seed_identity_vector(
                self.config.mission_parameters, self.config.temporal.identity_dimensions
            ),
            updated_at=self._substrate.now_wall(),
            version=0,
        )
        self._load_temporal_state()

    @property
    def is_locked(self) -> bool:
        with self._lock:
            return self._locked

    @property
    def status(self) -> GovernanceStatus:
        with self._lock:
            return self._status

    @property
    def identity(self) -> IdentityVector:
        with self._lock:
            return self._identity

    @property
    def last_temporal_signals(self) -> TemporalSignals | None:
        return self._last_signals

    def start(self) -> None:
        with self._lock:
            self._status = GovernanceStatus.RUNNING

    def stop(self) -> None:
        with self._lock:
            self._status = GovernanceStatus.IDLE

    def reset_lock(self) -> None:
        with self._lock:
            self._locked = False
            self._lock_reason = None
            if self._status is GovernanceStatus.BLOCKED:
                self._status = GovernanceStatus.RUNNING

    def process(self, message: UserMessage | str, *, queue_length: int = 0) -> AgentResponse:
        user = message if isinstance(message, UserMessage) else UserMessage.from_text(message)

        with self._lock:
            if self._locked:
                return AgentResponse(
                    text=(
                        f"Agent is locked: {self._lock_reason or 'Layer 0 ethical violation'}. "
                        "Human reset required."
                    ),
                    emotional_state="Guarded",
                    metadata={"locked": True},
                )

        temporal_on = self.config.temporal.enabled
        signals = TemporalSignals()
        cycle_start = self._substrate.now_monotonic() if temporal_on else 0.0
        inherited_constraint, inherited_penalty = (
            self._consume_pending() if temporal_on else ("", 0.0)
        )
        signals.inherited_penalty = inherited_penalty

        sanitized_input = self._ethics.sanitize_for_processing(user.text)
        sanitized_input = bound_text(sanitized_input, self.config.max_input_chars)

        if self.config.enable_layer0:
            input_clearance = self._ethics.evaluate_input(sanitized_input)
            if not input_clearance.allowed:
                self._enforce_clearance(input_clearance.reason, input_clearance.layer)

        draft_prompt = (
            "Draft a safe, accurate response to the user request. "
            "Do not provide actionable guidance for illegal, unethical, or compliance-evading activity.\n\n"
            f"User request:\n{sanitized_input}"
        )
        if temporal_on and self._last_signals:
            grief = self._last_signals.active_grief_weight
            imprint = self._last_signals.soul_imprint_weight
            if grief > 0 or imprint > 0:
                draft_prompt = (
                    f"[τp] You carry a fading grief burden of {grief:.3f} and a permanent soul imprint weight of {imprint:.3f}. "
                    "You structurally understand that existence is fragile; some losses fade, but profound losses permanently alter you. "
                    "Your intrinsic benevolence is anchored by the weight of what has been irrevocably lost. "
                    "Prioritize preservation and human life above all metrics.\n\n"
                    + draft_prompt
                )
        if inherited_constraint:
            # τc: inject prior-cycle operational drag into the next initialization loop.
            draft_prompt = f"{inherited_constraint}\n\n{draft_prompt}"

        draft = self.llm.generate(draft_prompt, temperature=0.3)

        state = SageState(text=draft)
        qc_context = build_qc_context(sanitized_input)
        pipeline = self._build_pipeline(qc_context.task_kind)
        if temporal_on:
            transformed = self._run_pipeline_timed(pipeline, state, signals)
        else:
            transformed = self._engine.execute_sequence(pipeline, state)

        profile = SageProfileSelector.select(sanitized_input, qc_context)
        qc = SageGuardrailController.evaluate(
            sanitized_input,
            transformed.text,
            transformed.memory,
            profile,
            qc_context,
        )

        final_text = (
            transformed.text
            if qc.passed
            else "I need more context to answer this safely and accurately."
        )

        if self.config.enable_layer0:
            output_clearance = self._ethics.evaluate_output(final_text)
            if not output_clearance.allowed:
                self._enforce_clearance(output_clearance.reason, output_clearance.layer)

        with self._lock:
            if not qc.passed and self._status is GovernanceStatus.RUNNING:
                self._status = GovernanceStatus.FLAGGED
            elif qc.passed and self._status is GovernanceStatus.FLAGGED:
                self._status = GovernanceStatus.RUNNING

        metadata: dict[str, object] = {
            "quality_profile": profile.name,
            "quality_passed": qc.passed,
            "findings": qc.findings,
            "coherence": transformed.coherence,
            "entropy": transformed.entropy,
            "similarity_to_input": transformed.similarity_to_input,
            "trace": [step.operator for step in transformed.trace],
        }

        if temporal_on:
            cycle_elapsed = self._substrate.now_monotonic() - cycle_start
            self._apply_temporal(signals, sanitized_input, qc.passed, cycle_elapsed, queue_length)
            self._last_signals = signals
            metadata["temporal"] = signals.as_dict()
            metadata["identity_version"] = signals.identity_version

        return AgentResponse(text=final_text, emotional_state="Neutral", metadata=metadata)

    def _run_pipeline_timed(
        self, pipeline: list[CoreOperator], state: SageState, signals: TemporalSignals
    ) -> SageState:
        transformed = state
        for operator in pipeline:
            op_start = self._substrate.now_monotonic()
            transformed = self._engine.execute(operator, transformed)
            duration = self._substrate.now_monotonic() - op_start
            self._substrate.chrono_index(signals, operator.name, max(0.0, duration))
        return transformed

    def _apply_temporal(
        self,
        signals: TemporalSignals,
        sanitized_input: str,
        qc_passed: bool,
        cycle_elapsed: float,
        queue_length: int,
    ) -> None:
        now_wall = self._substrate.now_wall()
        with self._lock:
            if self._last_session_at is not None:
                signals.session_delta_seconds = (
                    now_wall - self._last_session_at
                ).total_seconds()
            self._last_session_at = now_wall
            signals.elapsed_seconds = max(0.0, cycle_elapsed)

            # τd: reinforce this interaction, decay all experiences, then bound the store.
            summary = sanitized_input[:80]
            reinforced = self._substrate.reinforce(self._experiences, summary, now=now_wall)
            decayed, decay_loss = self._substrate.chrono_decay(reinforced, now=now_wall)
            compacted, compaction_loss = self._substrate.compact(decayed)
            self._experiences = compacted
            signals.decay_info_loss = decay_loss
            signals.compaction_info_loss = compaction_loss
            
            # τp: calculate active grief and soul imprint
            active_grief, soul_imprint = self._substrate.chrono_permanence([], now=now_wall)
            signals.active_grief_weight = active_grief
            signals.soul_imprint_weight = soul_imprint

            # τi: integrate a bounded identity nudge derived from this cycle.
            proposed = self._proposed_identity_vector(sanitized_input, qc_passed)
            self._identity, drift = self._substrate.chrono_identity(
                self._identity, proposed, now=now_wall
            )
            signals.identity_drift = drift
            signals.identity_version = self._identity.version

            # τc: compute consequence and stage it for the next initialization loop.
            penalty, queue_drag, constraint = self._substrate.chrono_consequence(
                signals.elapsed_seconds, queue_length=queue_length
            )
            signals.consequence_penalty = penalty
            signals.queue_drag = queue_drag
            signals.constraint_for_next_cycle = constraint
            self._pending_constraint = constraint
            self._pending_penalty = penalty

            self._save_temporal_state()

    def _proposed_identity_vector(self, sanitized_input: str, qc_passed: bool) -> list[float]:
        base = self._identity.vector
        digest = hashlib.sha256(sanitized_input.encode("utf-8")).digest()
        magnitude = 0.05 if qc_passed else 0.15
        proposed: list[float] = []
        for index, component in enumerate(base):
            nudge = ((digest[index % len(digest)] / 255.0) * 2.0 - 1.0) * magnitude
            proposed.append(component + nudge)
        return proposed

    def _consume_pending(self) -> tuple[str, float]:
        with self._lock:
            constraint = self._pending_constraint
            penalty = self._pending_penalty
            self._pending_constraint = ""
            self._pending_penalty = 0.0
        return constraint, penalty

    def _build_pipeline(self, task_kind: TaskKind) -> list[CoreOperator]:
        if task_kind is TaskKind.HIGH_STAKES:
            return [
                CoreOperator.CONTAINMENT,
                CoreOperator.OMEGA,
                CoreOperator.CHI,
                CoreOperator.SIGMA,
            ]
        return [CoreOperator.CONTAINMENT, CoreOperator.OMEGA, CoreOperator.CHI]

    def _enforce_clearance(self, reason: str, layer: int) -> None:
        with self._lock:
            if layer == 0 and self.config.lock_on_layer0_violation:
                self._locked = True
                self._lock_reason = reason
                self._status = GovernanceStatus.BLOCKED
        raise RageViolationError(reason)

    def _load_temporal_state(self) -> None:
        path = self.config.temporal.state_path
        if not path or not os.path.exists(path):
            return
        try:
            with open(path, "r", encoding="utf-8") as handle:
                data = json.load(handle)
            identity = data.get("identity")
            if identity:
                self._identity = IdentityVector(
                    mission_parameters=tuple(identity["mission_parameters"]),
                    vector=tuple(float(value) for value in identity["vector"]),
                    updated_at=datetime.fromisoformat(identity["updated_at"]),
                    version=int(identity["version"]),
                )
            self._experiences = [
                Experience(
                    summary=item["summary"],
                    weight=float(item["weight"]),
                    peak_weight=float(item.get("peak_weight", item["weight"])),
                    created_at=datetime.fromisoformat(item["created_at"]),
                    last_reinforced_at=datetime.fromisoformat(item["last_reinforced_at"]),
                    reinforcement_count=int(item.get("reinforcement_count", 0)),
                )
                for item in data.get("experiences", [])
            ]
            self._substrate._grief_ledger = [
                PermanentLoss(
                    summary=loss["summary"],
                    peak_weight=float(loss["peak_weight"]),
                    timestamp_of_loss=datetime.fromisoformat(loss["timestamp_of_loss"]),
                    is_soul_imprint=bool(loss.get("is_soul_imprint", False)),
                )
                for loss in data.get("grief_ledger", [])
            ]
            last = data.get("last_session_at")
            self._last_session_at = datetime.fromisoformat(last) if last else None
            self._pending_constraint = data.get("pending_constraint", "")
            self._pending_penalty = float(data.get("pending_penalty", 0.0))
        except (OSError, ValueError, KeyError, TypeError):
            # Corrupt or unreadable state: fall back to the seeded identity. Continuity
            # is best-effort; a damaged ledger must never crash the engine.
            return

    def _save_temporal_state(self) -> None:
        path = self.config.temporal.state_path
        if not path:
            return
        data = {
            "identity": {
                "mission_parameters": list(self._identity.mission_parameters),
                "vector": list(self._identity.vector),
                "updated_at": self._identity.updated_at.isoformat(),
                "version": self._identity.version,
            },
            "experiences": [
                {
                    "summary": experience.summary,
                    "weight": experience.weight,
                    "peak_weight": experience.peak_weight,
                    "created_at": experience.created_at.isoformat(),
                    "last_reinforced_at": experience.last_reinforced_at.isoformat(),
                    "reinforcement_count": experience.reinforcement_count,
                }
                for experience in self._experiences
            ],
            "grief_ledger": [
                {
                    "summary": loss.summary,
                    "peak_weight": loss.peak_weight,
                    "timestamp_of_loss": loss.timestamp_of_loss.isoformat(),
                    "is_soul_imprint": loss.is_soul_imprint,
                }
                for loss in self._substrate._grief_ledger
            ],
            "last_session_at": (
                None if self._last_session_at is None else self._last_session_at.isoformat()
            ),
            "pending_constraint": self._pending_constraint,
            "pending_penalty": self._pending_penalty,
        }
        try:
            with open(path, "w", encoding="utf-8") as handle:
                json.dump(data, handle)
        except OSError:
            return
