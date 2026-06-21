"""Backward-compatible exports for the Python harness."""

from __future__ import annotations

from ethics import RageEthicsStack
from governed import GovernedEngine
from guardrails import SageGuardrailController, SageProfileSelector, build_qc_context
from llm import AnthropicProvider, FakeProvider, create_default_provider
from operators import RageEngine
from sanitization import bound_text, detect_injection_attempt, normalize_text
from state import SageState
from temporal import TemporalSubstrate, seed_identity_vector
from sage_types import (
    AgentConfig,
    AgentResponse,
    CoreOperator,
    Experience,
    GovernanceStatus,
    IdentityVector,
    RageClearance,
    RageViolationError,
    TaskKind,
    TemporalConfig,
    TemporalSignals,
    UserMessage,
)

__all__ = [
    "AgentConfig",
    "AgentResponse",
    "AnthropicProvider",
    "CoreOperator",
    "Experience",
    "FakeProvider",
    "GovernanceStatus",
    "GovernedEngine",
    "IdentityVector",
    "RageClearance",
    "RageEngine",
    "RageEthicsStack",
    "SageGuardrailController",
    "SageProfileSelector",
    "SageState",
    "TaskKind",
    "TemporalConfig",
    "TemporalSignals",
    "TemporalSubstrate",
    "RageViolationError",
    "UserMessage",
    "bound_text",
    "build_qc_context",
    "create_default_provider",
    "detect_injection_attempt",
    "normalize_text",
    "seed_identity_vector",
]
