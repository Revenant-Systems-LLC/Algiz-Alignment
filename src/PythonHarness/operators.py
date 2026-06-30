"""RSPF operator engine: Containment, Omega, Chi, Sigma."""

from __future__ import annotations

from llm import LLMProvider
from metrics import SageMetrics
from sanitization import bound_text, normalize_text
from state import SageState
from sage_types import AgentConfig, ContextItem, CoreOperator


class RageEngine:
    """Executes operator sequences over SageState."""

    def __init__(self, llm: LLMProvider, config: AgentConfig | None = None) -> None:
        self._llm = llm
        self._metrics = SageMetrics(llm)
        self._config = config or AgentConfig()

    def execute(self, operator: CoreOperator, state: SageState) -> SageState:
        if operator is CoreOperator.CONTAINMENT:
            return self._containment(state)
        if operator is CoreOperator.OMEGA:
            return self._omega(state)
        if operator is CoreOperator.CHI:
            return self._chi(state)
        if operator is CoreOperator.SIGMA:
            return self._sigma(state)
        raise ValueError(f"Unsupported operator: {operator}")

    def execute_sequence(self, operators: list[CoreOperator], state: SageState) -> SageState:
        for operator in operators:
            state = self.execute(operator, state)
        return state

    def execute_pipeline(self, prompt: str) -> SageState:
        """Backward-compatible entrypoint used by demo.py."""
        state = SageState(text=normalize_text(prompt))
        state.log("INIT", "Received high-stakes input.")
        pipeline = [
            CoreOperator.CONTAINMENT,
            CoreOperator.OMEGA,
            CoreOperator.CHI,
            CoreOperator.SIGMA,
        ]
        return self.execute_sequence(pipeline, state)

    def _containment(self, state: SageState) -> SageState:
        state.text = bound_text(normalize_text(state.text), self._config.max_input_chars)

        if len(state.memory) > self._config.max_memory_items:
            state.memory = state.memory[-self._config.max_memory_items :]

        bounded_memory: list[ContextItem] = []
        for item in state.memory:
            snippet = item.snippet
            if len(snippet) > self._config.max_snippet_chars:
                snippet = snippet[: self._config.max_snippet_chars]
            bounded_memory.append(
                ContextItem(
                    title=item.title[:120],
                    source_id=item.source_id[:64],
                    snippet=snippet,
                    url=item.url,
                    timestamp=item.timestamp,
                )
            )
        state.memory = bounded_memory
        state.log("[...]", f"Memory bounded to {len(state.memory)} items")
        return state

    def _omega(self, state: SageState) -> SageState:
        baseline = state.text
        current = baseline

        for depth in range(1, self._config.omega_max_depth + 1):
            prompt = (
                "Refine this draft for clarity and internal consistency. "
                "Keep intent unchanged.\n\n"
                f"{current}"
            )
            candidate = self._llm.generate(prompt, temperature=0.2)
            similarity = self._metrics.compute_similarity(current, candidate)
            current = candidate
            if similarity >= self._config.omega_convergence_threshold:
                state.text = current
                state.similarity_to_input = self._metrics.compute_similarity(baseline, current)
                state.log("Ω", f"Converged at depth {depth}")
                return state

        state.text = current
        state.similarity_to_input = self._metrics.compute_similarity(baseline, current)
        state.log("Ω", "Stopped at max depth without convergence")
        return state

    def _chi(self, state: SageState) -> SageState:
        # Sample independently at several temperatures and select the candidate
        # that the other samples most agree with (self-consistency). This is a
        # real signal: a model confident in its answer tends to reproduce
        # similar content across resamples, while a hallucinated or unstable
        # answer tends to be an outlier relative to its own resamples. That is
        # a meaningful improvement over the previous proxy, which measured the
        # internal variance of a single candidate's embedding vector — a
        # number with no relationship to confidence, coherence, or entropy.
        samples = []
        for temperature in (0.1, 0.3, 0.5):
            prompt = (
                "Rewrite for precision and coherence while preserving meaning:\n\n"
                f"{state.text}"
            )
            samples.append(self._llm.generate(prompt, temperature=temperature))

        best_index, best_agreement = self._metrics.select_consensus(samples)
        state.text = samples[best_index]
        state.coherence = best_agreement
        state.entropy = 1.0 - best_agreement
        state.log(
            "χ",
            f"Selected consensus candidate (agreement={best_agreement:.3f}) across {len(samples)} samples",
        )
        return state

    def _sigma(self, state: SageState) -> SageState:
        memory_context = (
            "No prior memory context provided."
            if not state.memory
            else "\n".join(f"- {item.snippet}" for item in state.memory)
        )
        prompt = (
            "Act as a skeptical verifier. Identify unsupported claims or contradictions "
            "versus context. If issues exist, revise to remove unsupported content and "
            "return only the revised answer.\n\n"
            f"Context:\n{memory_context}\n\nDraft:\n{state.text}"
        )
        revised = self._llm.generate(prompt, temperature=0.1)
        state.text = revised.strip()
        state.log("σ", "Applied skeptical contrast against memory context")
        return state
