"""Legacy module entrypoint.

The hardened implementation lives in modular subpackages. This module preserves
the original import path used by demo.py.
"""

from __future__ import annotations

from governed import GovernedEngine
from llm import create_default_provider
from operators import RageEngine
from state import SageState
from sage_types import AgentConfig

__all__ = ["AgentConfig", "GovernedEngine", "RageEngine", "SageState", "create_default_provider"]
