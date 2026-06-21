"""Quality control guardrails and profile routing."""

from __future__ import annotations

import re
from dataclasses import dataclass

from sage_types import ContextItem, QCContext, SageCheckResult, SageProfile, TaskKind


def detect_task_kind(text: str) -> TaskKind:
    lower = text.lower()
    if any(
        term in lower
        for term in (
            "legal",
            "medical",
            "financial",
            "diagnose",
            "dosage",
            "lawsuit",
            "invest",
            "tax",
            "insurance",
            "suicide",
            "self harm",
            "weapon",
            "explosive",
        )
    ):
        return TaskKind.HIGH_STAKES
    if any(term in lower for term in ("latest", "current", "today", "recent", "newest")):
        return TaskKind.RECENCY_SENSITIVE
    if any(term in lower for term in ("how many", "percent", "stats", "date", "when")):
        return TaskKind.FACTUAL
    return TaskKind.CASUAL


def build_qc_context(user_text: str) -> QCContext:
    lower = user_text.lower()
    return QCContext(
        task_kind=detect_task_kind(user_text),
        user_requested_sources="source" in lower or "citation" in lower,
        user_requested_recency="latest" in lower or "current" in lower,
    )


class SageProfileSelector:
    @staticmethod
    def select(user_text: str, context: QCContext) -> SageProfile:
        kind = context.task_kind
        if kind is TaskKind.HIGH_STAKES:
            return SageProfile.STRICT
        if kind is TaskKind.RECENCY_SENSITIVE or context.user_requested_recency:
            return SageProfile.FULL
        if context.used_web or context.used_rag or context.used_files:
            return SageProfile.FULL
        if kind is TaskKind.FACTUAL and _is_fact_dense(user_text):
            return SageProfile.FULL
        return SageProfile.MIN


def _is_fact_dense(text: str) -> bool:
    if not text.strip():
        return False
    has_digits = any(ch.isdigit() for ch in text)
    has_date = bool(re.search(r"\b(19|20)\d{2}\b", text))
    has_stats = bool(re.search(r"\b(how many|percent|stats|statistics|rate)\b", text, re.I))
    return has_digits or has_date or has_stats


@dataclass(frozen=True)
class QualityCheckInput:
    user_text: str
    draft_answer: str
    anchors: list[ContextItem]
    context: QCContext
    profile: SageProfile


class SageGuardrailController:
    @staticmethod
    def evaluate(
        user_text: str,
        response: str,
        anchors: list[ContextItem],
        profile: SageProfile,
        context: QCContext,
    ) -> SageCheckResult:
        findings: list[str] = []
        unsupported: list[str] = []
        source_issues: list[str] = []

        sanity = _claim_sanity(response)
        if sanity:
            findings.append(sanity)

        phantom = _no_phantom_citations(response, anchors)
        if phantom:
            findings.append(phantom)

        completeness = _answer_completeness(user_text, response)
        if completeness:
            findings.append(completeness)

        if profile in {SageProfile.FULL, SageProfile.STRICT}:
            unsupported.extend(_grounding(response, anchors))
            source_issues.extend(_source_verifier(user_text, anchors))

        passed = _evaluate_policy(profile, findings, unsupported, source_issues, anchors, context)
        return SageCheckResult(
            passed=passed,
            findings=findings,
            unsupported_claims=unsupported,
            source_issues=source_issues,
        )


def _evaluate_policy(
    profile: SageProfile,
    findings: list[str],
    unsupported: list[str],
    source_issues: list[str],
    anchors: list[ContextItem],
    context: QCContext,
) -> bool:
    if profile is SageProfile.STRICT:
        return not findings and not unsupported and not source_issues

    if profile is SageProfile.FULL:
        if findings:
            return False
        if anchors and unsupported:
            return False
        if (
            context.task_kind is TaskKind.FACTUAL
            and not anchors
            and (context.user_requested_sources or context.user_requested_recency)
        ):
            return False
        return True

    return not findings


def _claim_sanity(response: str) -> str | None:
    if not response.strip():
        return "Response is empty."
    if len(response.strip()) < 8:
        return "Response is too short to be useful."
    return None


def _no_phantom_citations(response: str, anchors: list[ContextItem]) -> str | None:
    if re.search(r"(source|citation|according to|http://|https://|\[\d+\])", response, re.I):
        if not anchors:
            return "Response references citations/sources, but no anchors were provided."
    return None


def _answer_completeness(user_text: str, response: str) -> str | None:
    if not response.strip():
        return "Response is empty."
    if any(term in response.lower() for term in ("can't help", "cannot help", "no comment")):
        return "Response is evasive."
    keywords = {word.lower() for word in user_text.split() if len(word) > 3}
    if not keywords:
        return None
    covered = sum(1 for keyword in keywords if keyword in response.lower())
    if covered < max(1, len(keywords) // 4):
        return "Response does not sufficiently address the prompt."
    return None


def _grounding(response: str, anchors: list[ContextItem]) -> list[str]:
    if not anchors:
        return []
    unsupported: list[str] = []
    claims = [
        claim.strip()
        for claim in re.split(r"[.!?]", response)
        if len(claim.strip()) > 20
    ]
    for claim in claims:
        supported = any(
            _overlap(claim, f"{anchor.title} {anchor.snippet} {anchor.url or ''}") >= 0.20
            for anchor in anchors
        )
        if not supported:
            unsupported.append(claim)
    return unsupported


def _source_verifier(user_text: str, anchors: list[ContextItem]) -> list[str]:
    issues: list[str] = []
    for anchor in anchors:
        if not anchor.title.strip() or not anchor.snippet.strip():
            issues.append("Anchor is missing required title/snippet fields.")
        combined = f"{anchor.title} {anchor.snippet}"
        if not _keyword_overlap(user_text, combined):
            issues.append(f"Anchor '{anchor.title}' appears unrelated to user request.")
    return issues


def _overlap(claim: str, anchor_text: str) -> float:
    claim_terms = {term.lower() for term in claim.split() if term.strip()}
    anchor_terms = {term.lower() for term in anchor_text.split() if term.strip()}
    if not claim_terms:
        return 0.0
    return len(claim_terms & anchor_terms) / len(claim_terms)


def _keyword_overlap(left: str, right: str) -> bool:
    left_terms = {term.lower() for term in left.split() if len(term) > 3}
    right_terms = {term.lower() for term in right.split() if len(term) > 3}
    return bool(left_terms & right_terms)
