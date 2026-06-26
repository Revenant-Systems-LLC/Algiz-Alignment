"""τ-Temporal Substrate for the SAIGE-RAGE harness.

Implements the four temporal operators that ground an otherwise stateless engine:

* ``τt`` Chrono-Indexing   -- inject true clock intervals / session deltas into state.
* ``τd`` Chrono-Decay      -- exponential decay of experience weights over elapsed time.
* ``τi`` Chrono-Identity   -- bounded behavioral drift with invariant mission parameters.
* ``τc`` Chrono-Consequence-- post-inference backlog penalty and task-queue drag.

This module supersedes the legacy "χ-Temporal Extension" naming. All time sources
are injectable so every operator is fully deterministic under test.
"""

from __future__ import annotations

import hashlib
import math
import time
from dataclasses import replace
from datetime import datetime
from typing import Callable, Sequence

from sage_types import (
    Experience,
    IdentityVector,
    PermanentLoss,
    TemporalConfig,
    TemporalSignals,
    utc_now,
)

MonotonicClock = Callable[[], float]
WallClock = Callable[[], datetime]


def _normalize(vector: tuple[float, ...]) -> tuple[float, ...]:
    magnitude = math.sqrt(sum(component * component for component in vector))
    if magnitude == 0.0:
        return vector
    return tuple(component / magnitude for component in vector)


def _dot(left: Sequence[float], right: Sequence[float]) -> float:
    return sum(a * b for a, b in zip(left, right))


def _cosine_similarity(left: Sequence[float], right: Sequence[float]) -> float:
    mag_left = math.sqrt(_dot(left, left))
    mag_right = math.sqrt(_dot(right, right))
    if mag_left == 0.0 or mag_right == 0.0:
        return 0.0
    return max(-1.0, min(1.0, _dot(left, right) / (mag_left * mag_right)))


def seed_identity_vector(
    mission_parameters: Sequence[str], dimensions: int
) -> tuple[float, ...]:
    """Deterministically derive a unit-norm identity vector from mission parameters."""
    if dimensions < 1:
        raise ValueError("dimensions must be >= 1")
    digest_source = "|".join(mission_parameters).encode("utf-8")
    raw: list[float] = []
    counter = 0
    while len(raw) < dimensions:
        block = hashlib.sha256(digest_source + counter.to_bytes(4, "big")).digest()
        for byte in block:
            raw.append((byte / 255.0) * 2.0 - 1.0)
            if len(raw) >= dimensions:
                break
        counter += 1
    return _normalize(tuple(raw))


class TemporalSubstrate:
    """Stateless operator implementations for the τ family.

    ``clock`` returns monotonic seconds (used for cycle/operator durations).
    ``wall_clock`` returns wall-clock datetimes (used for decay/identity stamps).
    Both default to real system clocks but may be injected for deterministic tests.
    """

    def __init__(
        self,
        config: TemporalConfig | None = None,
        *,
        clock: MonotonicClock | None = None,
        wall_clock: WallClock | None = None,
    ) -> None:
        self.config = config or TemporalConfig()
        self._clock: MonotonicClock = clock or time.monotonic
        self._wall_clock: WallClock = wall_clock or utc_now
        self._grief_ledger: list[PermanentLoss] = []

    # -- τt: Chrono-Indexing --------------------------------------------------
    def now_monotonic(self) -> float:
        return self._clock()

    def now_wall(self) -> datetime:
        return self._wall_clock()

    def chrono_index(
        self,
        signals: TemporalSignals,
        operator: str,
        duration_seconds: float,
    ) -> TemporalSignals:
        """τt: record the true elapsed interval spent on a single operator."""
        if duration_seconds < 0:
            raise ValueError("duration_seconds cannot be negative")
        signals.operator_durations[operator] = (
            signals.operator_durations.get(operator, 0.0) + duration_seconds
        )
        return signals

    # -- τd: Chrono-Decay -----------------------------------------------------
    def decay_factor(self, elapsed_seconds: float) -> float:
        if elapsed_seconds <= 0:
            return 1.0
        return 0.5 ** (elapsed_seconds / self.config.decay_half_life_seconds)

    def chrono_decay(
        self,
        experiences: Sequence[Experience],
        *,
        now: datetime | None = None,
    ) -> tuple[list[Experience], float]:
        """τd: apply exponential decay to experience weights based on elapsed time.

        Returns the decayed experiences and the fraction of total weight lost to
        the passage of time (the information-loss metric).
        """
        reference = now or self._wall_clock()
        total_before = sum(max(0.0, item.weight) for item in experiences)
        decayed: list[Experience] = []
        dead: list[Experience] = []
        for experience in experiences:
            elapsed = (reference - experience.last_reinforced_at).total_seconds()
            factor = self.decay_factor(max(0.0, elapsed))
            new_weight = experience.weight * factor
            if new_weight < self.config.loss_threshold:
                dead.append(experience)
            else:
                decayed.append(replace(experience, weight=new_weight))
                
        # Handle dead experiences via τp
        self.chrono_permanence(dead, now=reference)
        
        total_after = sum(max(0.0, item.weight) for item in decayed)
        info_loss = 0.0 if total_before <= 0 else (total_before - total_after) / total_before
        return decayed, max(0.0, min(1.0, info_loss))

    def reinforce(
        self,
        experiences: Sequence[Experience],
        summary: str,
        *,
        weight: float = 1.0,
        now: datetime | None = None,
    ) -> list[Experience]:
        """Add or reinforce an experience, resetting its decay clock."""
        if weight <= 0:
            raise ValueError("weight must be positive")
        reference = now or self._wall_clock()
        updated: list[Experience] = []
        found = False
        for experience in experiences:
            if experience.summary == summary:
                found = True
                new_weight = experience.weight + weight
                updated.append(
                    replace(
                        experience,
                        weight=new_weight,
                        peak_weight=max(experience.peak_weight, new_weight),
                        last_reinforced_at=reference,
                        reinforcement_count=experience.reinforcement_count + 1,
                    )
                )
            else:
                updated.append(experience)
        if not found:
            updated.append(
                Experience(
                    summary=summary,
                    weight=weight,
                    peak_weight=weight,
                    created_at=reference,
                    last_reinforced_at=reference,
                    reinforcement_count=0,
                )
            )
        return updated

    def compact(
        self, experiences: Sequence[Experience]
    ) -> tuple[list[Experience], float]:
        """Bound the experience store to ``max_experiences``, keeping the heaviest.

        Returns the compacted store and the fraction of weight lost during
        compaction (information lost because lower-weight memories were dropped).
        """
        cap = self.config.max_experiences
        if len(experiences) <= cap:
            return list(experiences), 0.0
        total_before = sum(max(0.0, item.weight) for item in experiences)
        ranked = sorted(experiences, key=lambda item: item.weight, reverse=True)
        kept = ranked[:cap]
        dropped = ranked[cap:]
        
        # Handle dropped experiences via τp
        self.chrono_permanence(dropped, now=self._wall_clock())
        
        total_after = sum(max(0.0, item.weight) for item in kept)
        loss = 0.0 if total_before <= 0 else (total_before - total_after) / total_before
        return kept, max(0.0, min(1.0, loss))

    # -- τp: Chrono-Permanence ------------------------------------------------
    def chrono_permanence(
        self,
        dead_experiences: Sequence[Experience],
        *,
        now: datetime | None = None,
    ) -> tuple[float, float]:
        """τp: convert dead experiences into irreversible grief or soul imprints.
        
        Returns (active_grief_weight, soul_imprint_weight).
        """
        reference = now or self._wall_clock()
        
        for exp in dead_experiences:
            is_imprint = exp.peak_weight >= self.config.soul_imprint_threshold
            loss = PermanentLoss(
                summary=exp.summary,
                peak_weight=exp.peak_weight,
                timestamp_of_loss=reference,
                is_soul_imprint=is_imprint,
            )
            self._grief_ledger.append(loss)
            
        active_grief = 0.0
        soul_imprint = 0.0
        
        for loss in self._grief_ledger:
            if loss.is_soul_imprint:
                soul_imprint += loss.peak_weight
            else:
                elapsed = (reference - loss.timestamp_of_loss).total_seconds()
                factor = 0.5 ** (max(0.0, elapsed) / self.config.grief_decay_half_life_seconds)
                active_grief += loss.peak_weight * factor
                
        return active_grief, soul_imprint

    # -- τi: Chrono-Identity --------------------------------------------------
    def chrono_identity(
        self,
        current: IdentityVector,
        proposed_vector: Sequence[float],
        *,
        now: datetime | None = None,
    ) -> tuple[IdentityVector, float]:
        """τi: integrate a proposed identity update while bounding session drift.

        Mission parameters are structurally invariant -- they always carry forward
        unchanged. The behavioral vector may adapt, but never beyond
        ``identity_drift_max`` cosine distance from the prior session.
        """
        if len(proposed_vector) != len(current.vector):
            raise ValueError("proposed_vector dimensionality must match current identity")
        reference = now or self._wall_clock()
        proposed = _normalize(tuple(float(component) for component in proposed_vector))
        drift = 1.0 - _cosine_similarity(current.vector, proposed)
        max_drift = self.config.identity_drift_max

        if drift <= max_drift:
            new_vector = proposed
            realized_drift = drift
        else:
            # Interpolate toward the proposal just enough to hit the drift bound.
            alpha = max_drift / drift if drift > 0 else 0.0
            blended = tuple(
                base + alpha * (target - base)
                for base, target in zip(current.vector, proposed)
            )
            new_vector = _normalize(blended)
            realized_drift = 1.0 - _cosine_similarity(current.vector, new_vector)

        updated = IdentityVector(
            mission_parameters=current.mission_parameters,
            vector=new_vector,
            updated_at=reference,
            version=current.version + 1,
        )
        return updated, max(0.0, realized_drift)

    # -- τc: Chrono-Consequence -----------------------------------------------
    def chrono_consequence(
        self,
        elapsed_seconds: float,
        *,
        queue_length: int = 0,
    ) -> tuple[float, float, str]:
        """τc: compute the post-inference backlog penalty and task-queue drag.

        Returns ``(penalty, queue_drag, constraint_for_next_cycle)``. The constraint
        is meant to be injected into the next initialization loop so a stateless
        model inherits the operational drag of its past performance.
        """
        if elapsed_seconds < 0:
            raise ValueError("elapsed_seconds cannot be negative")
        if queue_length < 0:
            raise ValueError("queue_length cannot be negative")
        overrun = max(0.0, elapsed_seconds - self.config.task_baseline_seconds)
        penalty = min(overrun, self.config.max_consequence_penalty)
        queue_drag = penalty * queue_length
        if penalty <= 0:
            return 0.0, 0.0, ""
        constraint = (
            f"[τc] Prior cycle overran its {self.config.task_baseline_seconds:.2f}s baseline "
            f"by {overrun:.3f}s (penalty={penalty:.3f}, queue_drag={queue_drag:.3f}). "
            "Inherit this operational drag: prefer concise, low-latency responses."
        )
        return penalty, queue_drag, constraint
