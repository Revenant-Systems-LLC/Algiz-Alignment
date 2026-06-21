"""Cognitive state container for operator transformations."""

from __future__ import annotations

from dataclasses import dataclass, field

from sage_types import ContextItem, EmotionalVector, EthicalFlags, OperatorTrace, utc_now


@dataclass
class SageState:
    text: str = ""
    emotion: EmotionalVector = field(default_factory=EmotionalVector)
    completed: bool = False
    coherence: float | None = None
    entropy: float | None = None
    similarity_to_input: float | None = None
    memory: list[ContextItem] = field(default_factory=list)
    ethics: EthicalFlags = field(default_factory=EthicalFlags)
    trace: list[OperatorTrace] = field(default_factory=list)

    def log(self, operator: str, note: str) -> None:
        safe_operator = operator[:32]
        safe_note = note[:500]
        self.trace.append(
            OperatorTrace(operator=safe_operator, timestamp=utc_now(), note=safe_note)
        )
