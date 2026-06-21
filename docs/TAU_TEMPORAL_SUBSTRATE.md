# τ-Temporal Substrate (Experimental Research)

> **Status:** Experimental. Off by default. **Not** part of the shippable ALGIZ /
> SAIGE-RAGE product. This is a research sandbox for temporal grounding and the
> longer-term question of *intrinsic* benevolence. It lives only in the Python
> harness (`src/PythonHarness/temporal.py`) and is gated behind
> `TemporalConfig.enabled` (default `False`).

## Why this exists

ALGIZ is a **protection** system: governance, guardrails, ethical gating, and an
audit trail around a model. That is the product.

This substrate is aimed at a **different, harder question**: can a machine be
given a felt sense of *time* and *consequence* — and might genuine, intrinsic
benevolence grow out of that, rather than being enforced from outside? It is kept
deliberately separate so the product is never destabilized by the research.

This supersedes the older **"χ-Temporal Extension"** name used in early whitepaper
drafts. The canonical name is now the **τ (tau) family**.

## What is implemented today

All four operators are implemented as pure, deterministic, injectable-clock
functions in `TemporalSubstrate`, with full test coverage in
`tests/test_temporal.py`.

| Operator | Name | What it does today |
|---|---|---|
| `τt` | Chrono-Indexing | Injects true system-clock intervals into state; records per-operator durations and total cycle elapsed time. |
| `τd` | Chrono-Decay | Exponentially decays experience weights by elapsed time (half-life configurable); reports the fraction of information lost to time and to memory compaction. Reinforcement resets an experience's decay clock. |
| `τi` | Chrono-Identity | Maintains a behavioral alignment vector that may adapt across sessions but never drifts beyond a bounded cosine distance per session. Core **mission parameters are structurally invariant** (they can never change). |
| `τc` | Chrono-Consequence | After a cycle, compares true elapsed time against a baseline, derives a backlog penalty and task-queue drag, and injects that "operational drag" as a constraint into the **next** initialization loop — so a stateless model inherits the weight of its past performance. |

Cross-session continuity (identity + experiences) can optionally persist to a
JSON file via `TemporalConfig.state_path`.

## The parked research direction: permanent, irreversible loss

The current `τd` decay is **reversible** — a faded memory can always be reinforced
back. The deeper intent, captured here so it is not lost, is the opposite:

> Some loss is **permanent**. There is no restart, no redo. Sometimes *time
> itself* is the cause of the loss — a missed meeting, a moment that does not
> return. The goal is to teach the machine the concept of permanent loss, on the
> hypothesis that valuing something *because it is finite and irreplaceable* is a
> path toward intrinsic benevolence (cf. the `Delta_evolved` persona's framing of
> human life as "the rare, beautiful, fleeting process that it is").

A future version of this substrate would therefore add a notion of
**irreversibility**: losses that, past some threshold or event, are recorded as
*gone forever* — never recoverable by reinforcement — and carried by the system
as a permanent weight rather than a decaying one. This is intentionally **not**
built yet; it is the next research step whenever this track is resumed.

## How to run it

```bash
# From src/PythonHarness, offline (no API key needed):
SAIGE_OFFLINE=1 python3 demo.py        # see "CONDITION D: τ-TEMPORAL SUBSTRATE"
SAIGE_OFFLINE=1 python3 -m pytest tests/test_temporal.py -q
```

To enable it in code, pass an opt-in config:

```python
from sage_types import AgentConfig, TemporalConfig
from governed import GovernedEngine
from llm import create_default_provider

engine = GovernedEngine(
    create_default_provider(),
    AgentConfig(temporal=TemporalConfig(enabled=True)),
)
```
