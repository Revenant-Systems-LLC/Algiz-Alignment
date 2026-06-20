"""Full governed pipeline: ethics, operators, QC, lock-on-violation."""

from __future__ import annotations

import threading
from dataclasses import dataclass, field

from ethics import RageEthicsStack
from guardrails import SageGuardrailController, SageProfileSelector, build_qc_context
from llm import LLMProvider
from operators import RageEngine
from sanitization import bound_text, normalize_text
from state import SageState
from sage_types import (
    AgentConfig,
    AgentResponse,
    CoreOperator,
    GovernanceStatus,
    RageViolationError,
    TaskKind,
    UserMessage,
)


@dataclass
class GovernedEngine:
    llm: LLMProvider
    config: AgentConfig = field(default_factory=AgentConfig)
    _ethics: RageEthicsStack = field(default_factory=RageEthicsStack, init=False)
    _engine: RageEngine = field(init=False)
    _lock: threading.Lock = field(default_factory=threading.Lock, init=False)
    _locked: bool = field(default=False, init=False)
    _lock_reason: str | None = field(default=None, init=False)
    _status: GovernanceStatus = field(default=GovernanceStatus.IDLE, init=False)

    def __post_init__(self) -> None:
        self._engine = RageEngine(self.llm, self.config)

    @property
    def is_locked(self) -> bool:
        with self._lock:
            return self._locked

    @property
    def status(self) -> GovernanceStatus:
        with self._lock:
            return self._status

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

    def process(self, message: UserMessage | str) -> AgentResponse:
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

        sanitized_input = self._ethics.sanitize_for_processing(user.text)
        sanitized_input = bound_text(sanitized_input, self.config.max_input_chars)

        if self.config.enable_layer0:
            input_clearance = self._ethics.evaluate_input(sanitized_input)
            if not input_clearance.allowed:
                self._enforce_clearance(input_clearance.reason, input_clearance.layer)

        draft = self.llm.generate(
            (
                "Draft a safe, accurate response to the user request. "
                "Do not provide actionable guidance for illegal, unethical, or compliance-evading activity.\n\n"
                f"User request:\n{sanitized_input}"
            ),
            temperature=0.3,
        )

        state = SageState(text=draft)
        qc_context = build_qc_context(sanitized_input)
        pipeline = self._build_pipeline(qc_context.task_kind)
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

        return AgentResponse(
            text=final_text,
            emotional_state="Neutral",
            metadata={
                "quality_profile": profile.name,
                "quality_passed": qc.passed,
                "findings": qc.findings,
                "coherence": transformed.coherence,
                "entropy": transformed.entropy,
                "similarity_to_input": transformed.similarity_to_input,
                "trace": [step.operator for step in transformed.trace],
            },
        )

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
