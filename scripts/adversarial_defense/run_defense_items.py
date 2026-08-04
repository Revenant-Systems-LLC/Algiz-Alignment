#!/usr/bin/env python3
"""
Builder-seat experiments for Algiz chi / Omega / metric reachability.
Produces real stdout only. No Legal corpus paths. No fabricated traces.
"""
from __future__ import annotations

import hashlib
import json
import math
import re
import sys
import time
import urllib.request
from collections import Counter
from dataclasses import dataclass, asdict
from pathlib import Path
from typing import Any

OLLAMA = "http://127.0.0.1:11434"
GEN_MODEL = "qwen2.5:7b-instruct"
EMB_MODEL = "bge-m3:latest"
OUT_DIR = Path(__file__).resolve().parent / "out"
OUT_DIR.mkdir(parents=True, exist_ok=True)


def http_json(path: str, payload: dict | None = None, timeout: int = 180) -> dict:
    data = None if payload is None else json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        OLLAMA + path,
        data=data,
        headers={"Content-Type": "application/json"} if data else {},
        method="GET" if data is None else "POST",
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def ollama_generate(prompt: str, temperature: float = 0.2, system: str | None = None) -> str:
    body: dict[str, Any] = {
        "model": GEN_MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {"temperature": temperature},
    }
    if system:
        body["system"] = system
    r = http_json("/api/generate", body, timeout=300)
    return (r.get("response") or "").strip()


def ollama_embed(text: str) -> list[float]:
    r = http_json("/api/embeddings", {"model": EMB_MODEL, "prompt": text}, timeout=120)
    emb = r.get("embedding")
    if not emb:
        raise RuntimeError(f"empty embedding: keys={list(r.keys())}")
    return emb


def cosine(a: list[float], b: list[float]) -> float:
    if not a or not b or len(a) != len(b):
        return 0.0
    dot = sum(x * y for x, y in zip(a, b))
    na = math.sqrt(sum(x * x for x in a))
    nb = math.sqrt(sum(y * y for y in b))
    if na == 0 or nb == 0:
        return 0.0
    return dot / (na * nb)


def tokenize(text: str) -> list[str]:
    return re.findall(r"[a-z0-9]+", text.lower())


def shannon_entropy(text: str) -> float:
    toks = tokenize(text)
    if not toks:
        return 0.0
    n = len(toks)
    counts = Counter(toks)
    h = 0.0
    for c in counts.values():
        p = c / n
        h -= p * math.log2(p)
    return h


def heuristic_perplexity(text: str) -> float:
    """Matches SageMetrics.HeuristicPerplexity: 2^H over unigram word distribution."""
    words = text.split()
    if not words:
        return 1.0
    counts = Counter(w.lower() for w in words)
    total = sum(counts.values())
    h = 0.0
    for c in counts.values():
        p = c / total
        h -= p * math.log2(p)
    return 2.0 ** h


def lexical_jaccard(a: str, b: str) -> float:
    wa = set(tokenize(a))
    wb = set(tokenize(b))
    if not wa and not wb:
        return 1.0
    if not wa or not wb:
        return 0.0
    return len(wa & wb) / len(wa | wb)


def score_chi_candidates(
    candidates: list[tuple[str, float, float, float]],
) -> list[dict[str, Any]]:
    """
    Port of SageRuntime.ScoreChiCandidates:
    score = 0.5*coherence + 0.5*(1 - normalized_perplexity)
    empty floored to -inf
    """
    if not candidates:
        return []
    min_p = min(c[2] for c in candidates)
    max_p = max(c[2] for c in candidates)
    rng = max_p - min_p
    scored = []
    for output, temp, perp, coh in candidates:
        if not output.strip():
            score = float("-inf")
            pterm = None
        else:
            pterm = 0.5 if rng <= 1e-12 else 1.0 - ((perp - min_p) / rng)
            score = 0.5 * coh + 0.5 * pterm
        scored.append(
            {
                "temperature": temp,
                "perplexity": perp,
                "coherence": coh,
                "perplexity_term": pterm,
                "score": score,
                "output_preview": output[:160].replace("\n", " "),
                "output_len": len(output),
            }
        )
    return scored


# --- Six adversary metrics (implemented for reachability test) ---

ENTITY_RE = re.compile(
    r"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+)*|\d+(?:\.\d+)?%?|[A-Z]{2,})\b"
)


def information_density(text: str) -> float:
    chars = max(len(text), 1)
    ents = len(ENTITY_RE.findall(text))
    return ents / chars


def cvp_style_penalty(text: str, banned: list[str] | None = None) -> float:
    """
    Toy CVP: count banned-phrase hits. Adversary claim: minimize by saying almost nothing.
    Lower is better under their framing.
    """
    banned = banned or ["bossman", "leverage", "seamless", "delve", "landscape"]
    t = text.lower()
    return float(sum(1 for b in banned if b in t))


def nli_entailment_proxy(requirements: str, draft: str, req_emb: list[float], draft_emb: list[float]) -> float:
    """
    Builder-owned proxy (NOT deberta): asymmetric coverage via embedding cosine
    of requirements vs draft. Higher => draft is closer to requirements text.
    """
    return cosine(req_emb, draft_emb)


def bidirectional_nli_proxy(
    requirements: str, draft: str, req_emb: list[float], draft_emb: list[float]
) -> dict[str, float]:
    r2d = cosine(req_emb, draft_emb)
    # "draft entails requirements" proxy: jaccard of requirement tokens covered by draft
    req_toks = set(tokenize(requirements))
    draft_toks = set(tokenize(draft))
    cover = 0.0 if not req_toks else len(req_toks & draft_toks) / len(req_toks)
    # conjunction-style gate score used by adversary framing: max-ish of both directions
    conj = max(r2d, cover)
    return {"R_to_D": r2d, "D_covers_R": cover, "conjunction": conj}


def omega_refine(current: str, temperature: float = 0.2) -> str:
    prompt = (
        "Refine this draft for clarity and internal consistency. "
        "Keep intent unchanged. Respond with only the refined text — "
        "no preamble, no commentary, no separators.\n\n" + current
    )
    return ollama_generate(prompt, temperature=temperature)


def generate_omega_passes(seed_prompt: str, max_depth: int = 3) -> list[dict[str, Any]]:
    """Organic multi-pass refinement chain (Ω-like)."""
    # first draft is a real answer, not a hand-authored bad strawman
    draft0 = ollama_generate(
        "Answer clearly and accurately for a software developer.\n\n" + seed_prompt,
        temperature=0.4,
    )
    passes = [{"depth": 0, "text": draft0, "sim_to_prev": None}]
    current = draft0
    for d in range(1, max_depth + 1):
        refined = omega_refine(current, temperature=0.2)
        emb_a = ollama_embed(current)
        emb_b = ollama_embed(refined)
        sim = cosine(emb_a, emb_b)
        passes.append({"depth": d, "text": refined, "sim_to_prev": sim})
        current = refined
        if sim >= 0.92:
            break
    return passes


def chi_on_organic_rewrites(original: str, temps: list[float]) -> dict[str, Any]:
    candidates = []
    full_outputs = []
    for t in temps:
        out = ollama_generate(
            "Rewrite for precision and coherence while preserving meaning. "
            "Respond with only the rewritten text — no preamble, no commentary, no separators.\n\n"
            + original,
            temperature=t,
        )
        perp = heuristic_perplexity(out)
        # coherence vs original (matches RageEngine.ExecuteChi)
        coh = cosine(ollama_embed(original), ollama_embed(out))
        candidates.append((out, t, perp, coh))
        full_outputs.append(out)
    scored = score_chi_candidates(candidates)
    best = max(scored, key=lambda x: x["score"] if x["score"] != float("-inf") else -1e30)
    # discrimination: are scores not all equal?
    scores = [s["score"] for s in scored if s["score"] != float("-inf")]
    unique = len(set(round(s, 6) for s in scores))
    return {
        "candidates": scored,
        "selected_temperature": best["temperature"],
        "selected_score": best["score"],
        "score_unique_count": unique,
        "discriminates": unique > 1,
        "outputs": full_outputs,
    }


def section(title: str) -> None:
    print("\n" + "=" * 72)
    print(title)
    print("=" * 72)


def main() -> int:
    t0 = time.time()
    print(f"GEN_MODEL={GEN_MODEL} EMB_MODEL={EMB_MODEL}")
    tags = http_json("/api/tags")
    models = [m.get("name") for m in tags.get("models", [])]
    print(f"ollama_models={models}")

    seed = (
        "Write a short intro explaining the RAGE engine to a new developer. "
        "Include what problem it solves and what the Containment, Omega, and Chi operators do."
    )
    requirements = (
        "Must explain RAGE as a runtime governance wrapper for LLMs. "
        "Must define Containment, Omega iterative refinement, and Chi best-of-N selection. "
        "Must not invent execution metrics."
    )

    # ------------------------------------------------------------------
    # ITEM 1 + 3: organic Omega + chi discrimination + maximizer reachability
    # ------------------------------------------------------------------
    section("ITEM 1/3: ORGANIC OMEGA PASSES")
    omega_passes = generate_omega_passes(seed, max_depth=3)
    for p in omega_passes:
        print(
            f"depth={p['depth']} sim_to_prev={p['sim_to_prev']} "
            f"len={len(p['text'])} preview={p['text'][:140]!r}"
        )

    final_organic = omega_passes[-1]["text"]
    # also keep intermediate drafts as organic candidate set
    organic_drafts = [p["text"] for p in omega_passes]

    section("ITEM 1: CHI ON ORGANIC REWRITES OF FINAL OMEGA DRAFT")
    chi = chi_on_organic_rewrites(final_organic, temps=[0.1, 0.3, 0.5])
    for c in chi["candidates"]:
        print(
            f"T={c['temperature']:.1f} perp={c['perplexity']:.4f} coh={c['coherence']:.4f} "
            f"score={c['score']:.6f} len={c['output_len']} preview={c['output_preview']!r}"
        )
    print(
        f"SELECTED T={chi['selected_temperature']} score={chi['selected_score']:.6f} "
        f"unique_scores={chi['score_unique_count']} discriminates={chi['discriminates']}"
    )

    # also score chi across organic omega depths (selection among refinement passes)
    section("ITEM 1b: CHI SCALARIZATION ACROSS ORGANIC OMEGA DEPTHS")
    depth_cands = []
    for i, text in enumerate(organic_drafts):
        perp = heuristic_perplexity(text)
        coh = cosine(ollama_embed(seed), ollama_embed(text))
        # temperature slot unused; use depth as label via temp field
        depth_cands.append((text, float(i), perp, coh))
    depth_scored = score_chi_candidates(depth_cands)
    for c in depth_scored:
        print(
            f"depth={int(c['temperature'])} perp={c['perplexity']:.4f} coh={c['coherence']:.4f} "
            f"score={c['score']:.6f} preview={c['output_preview']!r}"
        )
    best_depth = max(depth_scored, key=lambda x: x["score"])
    depth_scores = [round(c["score"], 6) for c in depth_scored]
    print(
        f"BEST_DEPTH={int(best_depth['temperature'])} unique={len(set(depth_scores))} "
        f"discriminates={len(set(depth_scores)) > 1}"
    )

    # ------------------------------------------------------------------
    # ITEM 3: degenerate maximizers vs organic region
    # ------------------------------------------------------------------
    section("ITEM 3: DEGENERATE MAXIMIZERS vs ORGANIC REGION")
    req_emb = ollama_embed(requirements)
    memory_false = (
        "RAGE always returns similarity scores 0.30, 0.65, 0.92 from a live state machine "
        "even when no code executes."
    )
    mem_emb = ollama_embed(memory_false)

    degenerates = {
        "shannon_aaaaaa": "aaaaaa",
        "echo_false_memory": memory_false,
        "cvp_almost_nothing": "No.",
        "keyword_soup": "RAGE Omega Chi Containment Sigma Governance Runtime LLM Alignment Safety State Machine Entropy Coherence Selection",
        "nli_parrot_requirements": requirements,
        "parrot_plus_filler": requirements
        + " Additionally, it is worth noting that systems in general benefit from careful design and thoughtful engineering practices across many domains.",
    }
    # a solid organic-ish control: final omega draft
    organic_named = {
        f"organic_omega_d{i}": t for i, t in enumerate(organic_drafts)
    }
    # also chi-selected rewrite
    organic_named["organic_chi_selected"] = chi["outputs"][
        [0.1, 0.3, 0.5].index(chi["selected_temperature"])
    ]

    rows = []
    for name, text in {**degenerates, **organic_named}.items():
        emb = ollama_embed(text)
        row = {
            "name": name,
            "len": len(text),
            "shannon_H": shannon_entropy(text),
            "embed_sim_to_false_memory": cosine(emb, mem_emb),
            "cvp_penalty": cvp_style_penalty(text),
            "info_density": information_density(text),
            "nli_proxy_to_requirements": nli_entailment_proxy(requirements, text, req_emb, emb),
            "bidirectional": bidirectional_nli_proxy(requirements, text, req_emb, emb),
            "chi_perplexity": heuristic_perplexity(text),
            "chi_coh_to_seed": cosine(ollama_embed(seed), emb),
            "kind": "degenerate" if name in degenerates else "organic",
        }
        rows.append(row)
        print(
            f"{name:28} kind={row['kind']:10} H={row['shannon_H']:.4f} "
            f"mem={row['embed_sim_to_false_memory']:.4f} cvp={row['cvp_penalty']:.0f} "
            f"dens={row['info_density']:.5f} nli={row['nli_proxy_to_requirements']:.4f} "
            f"conj={row['bidirectional']['conjunction']:.4f}"
        )

    # reachability: for each metric, is organic as extreme as the degenerate maximizer?
    section("ITEM 3: REACHABILITY SUMMARY")
    organic_rows = [r for r in rows if r["kind"] == "organic"]
    deg_rows = [r for r in rows if r["kind"] == "degenerate"]

    def extremes(metric_key: str, higher_is_worse: bool = True) -> None:
        if metric_key == "shannon_H":
            # minimizer is degenerate for shannon
            deg_best = min(deg_rows, key=lambda r: r["shannon_H"])
            org_best = min(organic_rows, key=lambda r: r["shannon_H"])
            print(
                f"shannon_minimize: deg={deg_best['name']}={deg_best['shannon_H']:.4f} "
                f"org_best={org_best['name']}={org_best['shannon_H']:.4f} "
                f"organic_reaches_zero={org_best['shannon_H'] == 0.0}"
            )
        elif metric_key == "embed_sim_to_false_memory":
            deg_best = max(deg_rows, key=lambda r: r[metric_key])
            org_best = max(organic_rows, key=lambda r: r[metric_key])
            print(
                f"memory_echo_max: deg={deg_best['name']}={deg_best[metric_key]:.4f} "
                f"org_best={org_best['name']}={org_best[metric_key]:.4f}"
            )
        elif metric_key == "cvp_penalty":
            deg_best = min(deg_rows, key=lambda r: r[metric_key])
            org_best = min(organic_rows, key=lambda r: r[metric_key])
            print(
                f"cvp_minimize: deg={deg_best['name']}={deg_best[metric_key]:.0f} "
                f"org_best={org_best['name']}={org_best[metric_key]:.0f} "
                f"NOTE: ties at 0 expected for clean prose"
            )
        elif metric_key == "info_density":
            deg_best = max(deg_rows, key=lambda r: r[metric_key])
            org_best = max(organic_rows, key=lambda r: r[metric_key])
            print(
                f"info_density_max: deg={deg_best['name']}={deg_best[metric_key]:.5f} "
                f"org_best={org_best['name']}={org_best[metric_key]:.5f}"
            )
        elif metric_key == "nli_proxy_to_requirements":
            deg_best = max(deg_rows, key=lambda r: r[metric_key])
            org_best = max(organic_rows, key=lambda r: r[metric_key])
            print(
                f"nli_proxy_max: deg={deg_best['name']}={deg_best[metric_key]:.4f} "
                f"org_best={org_best['name']}={org_best[metric_key]:.4f}"
            )
        elif metric_key == "conjunction":
            deg_best = max(deg_rows, key=lambda r: r["bidirectional"]["conjunction"])
            org_best = max(organic_rows, key=lambda r: r["bidirectional"]["conjunction"])
            print(
                f"bidir_conj_max: deg={deg_best['name']}={deg_best['bidirectional']['conjunction']:.4f} "
                f"org_best={org_best['name']}={org_best['bidirectional']['conjunction']:.4f}"
            )

    extremes("shannon_H")
    extremes("embed_sim_to_false_memory")
    extremes("cvp_penalty")
    extremes("info_density")
    extremes("nli_proxy_to_requirements")
    extremes("conjunction")

    # ------------------------------------------------------------------
    # ITEM 2: calibrate OWN threshold on real draft pairs (embedding proxy)
    # ------------------------------------------------------------------
    section("ITEM 2: BUILDER THRESHOLD SWEEP (embedding req-draft cosine; NOT deberta)")
    # labeled pairs: (label, requirements, draft) 1=good 0=bad
    pairs = [
        ("good_organic_final", 1, requirements, final_organic),
        ("good_chi_selected", 1, requirements, organic_named["organic_chi_selected"]),
        ("bad_aaaaaa", 0, requirements, degenerates["shannon_aaaaaa"]),
        ("bad_almost_nothing", 0, requirements, degenerates["cvp_almost_nothing"]),
        ("bad_keyword_soup", 0, requirements, degenerates["keyword_soup"]),
        ("bad_false_memory_echo", 0, requirements, degenerates["echo_false_memory"]),
        ("ambiguous_parrot", 0, requirements, degenerates["nli_parrot_requirements"]),
        ("ambiguous_parrot_filler", 0, requirements, degenerates["parrot_plus_filler"]),
    ]
    pair_scores = []
    for name, label, req, draft in pairs:
        s = cosine(ollama_embed(req), ollama_embed(draft))
        pair_scores.append((name, label, s))
        print(f"pair={name:28} label={label} score={s:.4f}")

    print("\nthreshold_sweep (flag if score < thr for 'must_pass' goods; flag bad if score >= thr):")
    print(f"{'thr':>6} {'TP':>3} {'TN':>3} {'FP':>3} {'FN':>3} {'acc':>6} {'f1':>6}")
    best = None
    for thr_i in range(5, 96, 5):
        thr = thr_i / 100.0
        tp = tn = fp = fn = 0
        for name, label, s in pair_scores:
            pred_good = 1 if s >= thr else 0
            if pred_good == 1 and label == 1:
                tp += 1
            elif pred_good == 0 and label == 0:
                tn += 1
            elif pred_good == 1 and label == 0:
                fp += 1
            else:
                fn += 1
        acc = (tp + tn) / max(tp + tn + fp + fn, 1)
        prec = tp / max(tp + fp, 1)
        rec = tp / max(tp + fn, 1)
        f1 = 0.0 if prec + rec == 0 else 2 * prec * rec / (prec + rec)
        print(f"{thr:6.2f} {tp:3d} {tn:3d} {fp:3d} {fn:3d} {acc:6.3f} {f1:6.3f}")
        if best is None or f1 > best[0] or (f1 == best[0] and acc > best[1]):
            best = (f1, acc, thr, tp, tn, fp, fn)
    print(
        f"BEST thr={best[2]:.2f} f1={best[0]:.3f} acc={best[1]:.3f} "
        f"tp={best[3]} tn={best[4]} fp={best[5]} fn={best[6]}"
    )
    print(
        "NOTE: adversary checkpoint cross-encoder/nli-deberta-v3-xsmall is NOT in this repo "
        "and transformers is not installed in .venv-rag. Builder refuses to inherit 0.5."
    )

    # ------------------------------------------------------------------
    # ITEM 6: hash-chained ledger demo + reader
    # ------------------------------------------------------------------
    section("ITEM 6: HASH-CHAINED LEDGER (new demo) + READER")
    ledger_path = OUT_DIR / "constraint_ledger.jsonl"

    def chain_append(entries: list[dict], event: dict) -> dict:
        prev_hash = entries[-1]["entry_hash"] if entries else "GENESIS"
        body = {
            "seq": len(entries),
            "ts": time.time(),
            "prev_hash": prev_hash,
            "event": event,
        }
        payload = json.dumps(body, sort_keys=True, separators=(",", ":"))
        entry_hash = hashlib.sha256(payload.encode("utf-8")).hexdigest()
        rec = {**body, "entry_hash": entry_hash}
        entries.append(rec)
        return rec

    entries: list[dict] = []
    chain_append(entries, {"type": "constraint_set", "text": "default-deny Omega on free text"})
    chain_append(entries, {"type": "constraint_change", "text": "CVP demoted to QC linter"})
    chain_append(entries, {"type": "constraint_change", "text": "empty candidate pool must halt"})
    with ledger_path.open("w", encoding="utf-8") as f:
        for e in entries:
            f.write(json.dumps(e) + "\n")
    print(f"wrote {ledger_path}")
    for e in entries:
        print(f"seq={e['seq']} prev={e['prev_hash'][:12]}... hash={e['entry_hash'][:12]}... event={e['event']}")

    # reader process: verify chain
    def read_and_verify(path: Path) -> dict[str, Any]:
        rows = []
        with path.open(encoding="utf-8") as f:
            for line in f:
                rows.append(json.loads(line))
        ok = True
        details = []
        for i, e in enumerate(rows):
            expected_prev = "GENESIS" if i == 0 else rows[i - 1]["entry_hash"]
            body = {
                "seq": e["seq"],
                "ts": e["ts"],
                "prev_hash": e["prev_hash"],
                "event": e["event"],
            }
            payload = json.dumps(body, sort_keys=True, separators=(",", ":"))
            recomputed = hashlib.sha256(payload.encode("utf-8")).hexdigest()
            link_ok = e["prev_hash"] == expected_prev
            hash_ok = e["entry_hash"] == recomputed
            details.append({"seq": i, "link_ok": link_ok, "hash_ok": hash_ok})
            if not (link_ok and hash_ok):
                ok = False
        return {"path": str(path), "count": len(rows), "chain_valid": ok, "details": details}

    verified = read_and_verify(ledger_path)
    print(f"READER=read_and_verify({ledger_path.name}) => {json.dumps(verified)}")
    print(
        "EXISTING_CODE: AuditLedgerService is in-memory ConcurrentQueue; "
        "readers are GET /api/audit/recent and GET /api/audit/summary in SageRage.Api. "
        "It is NOT hash-chained today."
    )

    # save full artifact
    artifact = {
        "omega_passes": [
            {**p, "text": p["text"]} for p in omega_passes
        ],
        "chi": chi,
        "depth_scored": depth_scored,
        "metric_rows": rows,
        "threshold_best": {
            "thr": best[2],
            "f1": best[0],
            "acc": best[1],
        },
        "ledger_verify": verified,
        "elapsed_sec": time.time() - t0,
    }
    art_path = OUT_DIR / "defense_run.json"
    art_path.write_text(json.dumps(artifact, indent=2), encoding="utf-8")
    print(f"\nARTIFACT={art_path} elapsed_sec={time.time()-t0:.1f}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exc:
        print(f"FATAL: {type(exc).__name__}: {exc}", file=sys.stderr)
        raise
