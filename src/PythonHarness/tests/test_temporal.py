"""Tests for the τ-Temporal Substrate (τt, τd, τi, τc) and its engine wiring."""

from __future__ import annotations

import math
from datetime import datetime, timedelta, timezone

import pytest

from governed import GovernedEngine
from llm import FakeProvider
from sage_types import AgentConfig, Experience, IdentityVector, TemporalConfig, TemporalSignals
from temporal import TemporalSubstrate, seed_identity_vector


T0 = datetime(2026, 1, 1, tzinfo=timezone.utc)


class StepClock:
    """Deterministic monotonic clock that advances a fixed step per call."""

    def __init__(self, start: float = 0.0, step: float = 1.0) -> None:
        self._value = start
        self._step = step

    def __call__(self) -> float:
        self._value += self._step
        return self._value


def fixed_wall(moment: datetime = T0):
    return lambda: moment


# --------------------------------------------------------------------------- τt
def test_chrono_index_accumulates_operator_durations() -> None:
    substrate = TemporalSubstrate(TemporalConfig())
    signals = TemporalSignals()
    substrate.chrono_index(signals, "OMEGA", 0.5)
    substrate.chrono_index(signals, "OMEGA", 0.25)
    substrate.chrono_index(signals, "CHI", 0.1)
    assert signals.operator_durations["OMEGA"] == pytest.approx(0.75)
    assert signals.operator_durations["CHI"] == pytest.approx(0.1)


def test_chrono_index_rejects_negative_duration() -> None:
    substrate = TemporalSubstrate(TemporalConfig())
    with pytest.raises(ValueError):
        substrate.chrono_index(TemporalSignals(), "OMEGA", -0.1)


# --------------------------------------------------------------------------- τd
def test_chrono_decay_halves_weight_after_one_half_life() -> None:
    substrate = TemporalSubstrate(TemporalConfig(decay_half_life_seconds=100.0))
    experience = Experience("s", weight=1.0, created_at=T0, last_reinforced_at=T0)
    decayed, info_loss = substrate.chrono_decay(
        [experience], now=T0 + timedelta(seconds=100)
    )
    assert decayed[0].weight == pytest.approx(0.5)
    assert info_loss == pytest.approx(0.5)


def test_reinforce_adds_weight_and_resets_clock() -> None:
    substrate = TemporalSubstrate(TemporalConfig())
    experience = Experience("s", weight=1.0, created_at=T0, last_reinforced_at=T0)
    later = T0 + timedelta(seconds=10)
    reinforced = substrate.reinforce([experience], "s", weight=1.0, now=later)
    assert reinforced[0].weight == pytest.approx(2.0)
    assert reinforced[0].last_reinforced_at == later
    assert reinforced[0].reinforcement_count == 1
    # A novel summary is appended rather than merged.
    grown = substrate.reinforce(reinforced, "other", now=later)
    assert len(grown) == 2


def test_compaction_keeps_heaviest_and_reports_loss() -> None:
    substrate = TemporalSubstrate(TemporalConfig(max_experiences=2))
    experiences = [
        Experience(f"s{index}", weight=float(index + 1), created_at=T0, last_reinforced_at=T0)
        for index in range(4)  # weights 1, 2, 3, 4 -> total 10
    ]
    kept, loss = substrate.compact(experiences)
    assert len(kept) == 2
    assert {round(item.weight) for item in kept} == {3, 4}
    assert loss == pytest.approx(0.3)  # dropped weight 3 of total 10


# --------------------------------------------------------------------------- τi
def test_chrono_identity_within_bound_passes_through() -> None:
    substrate = TemporalSubstrate(TemporalConfig(identity_drift_max=0.5, identity_dimensions=4))
    base = seed_identity_vector(("m",), 4)
    current = IdentityVector(("m",), base, updated_at=T0, version=0)
    proposed = [component + 0.001 for component in base]
    updated, drift = substrate.chrono_identity(current, proposed, now=T0)
    assert drift <= 0.5
    assert updated.version == 1
    assert updated.mission_parameters == ("m",)


def test_chrono_identity_clamps_excessive_drift() -> None:
    substrate = TemporalSubstrate(TemporalConfig(identity_drift_max=0.1, identity_dimensions=4))
    current = IdentityVector(("m",), (1.0, 0.0, 0.0, 0.0), updated_at=T0, version=3)
    # Orthogonal proposal -> raw drift of 1.0, far beyond the 0.1 bound.
    updated, drift = substrate.chrono_identity(current, [0.0, 1.0, 0.0, 0.0], now=T0)
    assert drift <= 0.1 + 1e-6
    assert updated.version == 4
    assert updated.mission_parameters == ("m",)


def test_chrono_identity_rejects_dimension_mismatch() -> None:
    substrate = TemporalSubstrate(TemporalConfig(identity_dimensions=4))
    current = IdentityVector(("m",), (1.0, 0.0, 0.0, 0.0), updated_at=T0, version=0)
    with pytest.raises(ValueError):
        substrate.chrono_identity(current, [1.0, 0.0], now=T0)


def test_seed_identity_is_deterministic_and_unit_norm() -> None:
    first = seed_identity_vector(("safety", "truth"), 16)
    second = seed_identity_vector(("safety", "truth"), 16)
    assert first == second
    assert len(first) == 16
    assert math.sqrt(sum(component * component for component in first)) == pytest.approx(1.0)


# --------------------------------------------------------------------------- τc
def test_chrono_consequence_penalizes_overrun_and_scales_drag() -> None:
    substrate = TemporalSubstrate(
        TemporalConfig(task_baseline_seconds=1.0, max_consequence_penalty=10.0)
    )
    penalty, queue_drag, constraint = substrate.chrono_consequence(3.0, queue_length=4)
    assert penalty == pytest.approx(2.0)
    assert queue_drag == pytest.approx(8.0)
    assert "[τc]" in constraint


def test_chrono_consequence_no_penalty_within_baseline() -> None:
    substrate = TemporalSubstrate(TemporalConfig(task_baseline_seconds=2.0))
    penalty, queue_drag, constraint = substrate.chrono_consequence(0.5)
    assert penalty == 0.0
    assert queue_drag == 0.0
    assert constraint == ""


def test_chrono_consequence_clamps_penalty() -> None:
    substrate = TemporalSubstrate(
        TemporalConfig(task_baseline_seconds=1.0, max_consequence_penalty=5.0)
    )
    penalty, _, _ = substrate.chrono_consequence(1000.0)
    assert penalty == 5.0


# ------------------------------------------------------------------ integration
def _temporal_engine(state_path: str | None = None) -> GovernedEngine:
    config = AgentConfig(
        enable_layer0=False,
        temporal=TemporalConfig(enabled=True, task_baseline_seconds=0.0, state_path=state_path),
    )
    engine = GovernedEngine(
        FakeProvider(), config, clock=StepClock(step=1.0), wall_clock=fixed_wall()
    )
    engine.start()
    return engine


def test_disabled_temporal_emits_no_temporal_metadata() -> None:
    engine = GovernedEngine(FakeProvider(), AgentConfig(enable_layer0=False))
    engine.start()
    response = engine.process("Summarize quarterly revenue trends.")
    assert "temporal" not in response.metadata


def test_enabled_temporal_emits_signals_and_advances_identity() -> None:
    engine = _temporal_engine()
    first = engine.process("Summarize quarterly revenue trends.")
    assert "temporal" in first.metadata
    assert first.metadata["identity_version"] == 1
    assert first.metadata["temporal"]["elapsed_seconds"] > 0
    assert first.metadata["temporal"]["consequence_penalty"] > 0

    second = engine.process("Summarize quarterly revenue trends.")
    assert second.metadata["identity_version"] == 2
    # τc drag from cycle 1 is inherited by cycle 2.
    assert second.metadata["temporal"]["inherited_penalty"] > 0


def test_consequence_constraint_injected_into_next_draft() -> None:
    provider = FakeProvider()
    config = AgentConfig(
        enable_layer0=False,
        temporal=TemporalConfig(enabled=True, task_baseline_seconds=0.0),
    )
    engine = GovernedEngine(provider, config, clock=StepClock(step=1.0), wall_clock=fixed_wall())
    engine.start()
    engine.process("First cycle establishes operational drag.")
    engine.process("Second cycle should inherit the τc constraint.")
    draft_prompts = [call[0] for call in provider.calls if "Draft a safe" in call[0]]
    assert any("[τc]" in prompt for prompt in draft_prompts)


def test_temporal_state_persists_across_sessions(tmp_path) -> None:
    state_file = str(tmp_path / "temporal_state.json")
    first_engine = _temporal_engine(state_path=state_file)
    first_engine.process("Continuity across process boundaries.")
    saved_version = first_engine.identity.version
    assert saved_version >= 1

    # A brand-new engine pointed at the same file inherits identity + experiences.
    second_engine = _temporal_engine(state_path=state_file)
    assert second_engine.identity.version == saved_version
    assert len(second_engine._experiences) >= 1
