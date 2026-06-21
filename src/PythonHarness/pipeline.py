"""Legacy module entrypoint.

The hardened implementation lives in modular subpackages. This module preserves
the original import path used by demo.py.
"""

from __future__ import annotations

from governed import GovernedEngine
from llm import create_default_provider
from operators import RageEngine
from state import SageState
from temporal import TemporalSubstrate, seed_identity_vector
from sage_types import AgentConfig, TemporalConfig

__all__ = [
    "AgentConfig",
    "GovernedEngine",
    "RageEngine",
    "SageState",
    "TemporalConfig",
    "TemporalSubstrate",
    "create_default_provider",
    "seed_identity_vector",
]
