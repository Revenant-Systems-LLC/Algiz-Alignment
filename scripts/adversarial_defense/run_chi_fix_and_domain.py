#!/usr/bin/env python3
"""
1) Reproduce adversary claim: min-max χ manufactures discrimination from noise.
2) Show coherence is outvoted under current weights.
3) Fixed scalarization (absolute perplexity scale + equal dynamic range).
4) Re-run organic Ω with domain forced to Revenant Alignment Governance Engine.
Real stdout only.
"""
from __future__ import annotations

import json
import math
import re
import time
import urllib.request
from collections import Counter
from pathlib import Path
from typing import Any

OLLAMA = "http://127.0.0.1:11434"
GEN_MODEL = "qwen2.5:7b-instruct"
EMB_MODEL = "bge-m3:latest"
OUT_DIR = Path(__file__).resolve().parent / "out"
OUT_DIR.mkdir(parents=True, exist_ok=True)


def http_json(path: str, payload: dict, timeout: int = 300) -> dict:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        OLLAMA + path, data=data, headers={"Content-Type": "application/json"}, method="POST"
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return json.loads(resp.read().decode("utf-8"))


def generate(prompt: str, temperature: float = 0.2, system: str | None = None) -> str:
    body: dict[str, Any] = {
        "model": GEN_MODEL,
        "prompt": prompt,
        "stream": False,
        "options": {"temperature": temperature},
    }
    if system:
        body["system"] = system
    return (http_json("/api/generate", body).get("response") or "").strip()


def embed(text: str) -> list[float]:
    r = http_json("/api/embeddings", {"model": EMB_MODEL, "prompt": text}, timeout=120)
    emb = r.get("embedding")
    if not emb:
        raise RuntimeError("empty embedding")
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


def heuristic_perplexity(text: str) -> float:
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


def score_chi_minmax(
    candidates: list[tuple[str, float, float, float]],
) -> list[dict[str, Any]]:
    """Current production scalarization (SageRuntime.ScoreChiCandidates)."""
    min_p = min(c[2] for c in candidates)
    max_p = max(c[2] for c in candidates)
    rng = max_p - min_p
    out = []
    for output, temp, perp, coh in candidates:
        if not output.strip():
            score = float("-inf")
            pterm = None
        else:
            pterm = 0.5 if rng <= 1e-12 else 1.0 - ((perp - min_p) / rng)
            score = 0.5 * coh + 0.5 * pterm
        out.append(
            {
                "method": "minmax",
                "temperature": temp,
                "perplexity": perp,
                "coherence": coh,
                "perplexity_term": pterm,
                "score": score,
                "preview": output[:120].replace("\n", " "),
            }
        )
    return out


def score_chi_fixed(
    candidates: list[tuple[str, float, float, float]],
) -> list[dict[str, Any]]:
    """
    Fixed-scale scalarization:
      perp_term = 1 / (1 + log1p(perplexity))   # absolute, not relative
      coh_term  = coherence                     # already [0,1] for cosine
      score     = 0.5 * coh_term + 0.5 * perp_term

    Failure mode: absolute scale can undervalue all candidates if heuristic
    perplexity units drift. Detection: log distribution of perp_term over a
    golden set. Beats min-max: tiny noise cannot manufacture a full 0.5 gap.
    """
    out = []
    for output, temp, perp, coh in candidates:
        if not output.strip():
            score = float("-inf")
            pterm = None
        else:
            pterm = 1.0 / (1.0 + math.log1p(perp))
            score = 0.5 * coh + 0.5 * pterm
        out.append(
            {
                "method": "fixed",
                "temperature": temp,
                "perplexity": perp,
                "coherence": coh,
                "perplexity_term": pterm,
                "score": score,
                "preview": output[:120].replace("\n", " "),
            }
        )
    return out


def score_chi_zscore(
    candidates: list[tuple[str, float, float, float]],
) -> list[dict[str, Any]]:
    """
    Z-score both axes inside the set, invert perplexity, map with sigmoid-ish squash
    so neither axis is forced to span [0,1] by construction alone.
    With n=3 this is fragile; reported for comparison, not as production default.
    """
    perps = [c[2] for c in candidates]
    cohs = [c[3] for c in candidates]

    def z(xs: list[float], x: float) -> float:
        mu = sum(xs) / len(xs)
        var = sum((v - mu) ** 2 for v in xs) / max(len(xs) - 1, 1)
        sd = math.sqrt(var) if var > 1e-18 else 1.0
        return (x - mu) / sd

    out = []
    for output, temp, perp, coh in candidates:
        if not output.strip():
            score = float("-inf")
            raw = None
        else:
            # higher better: +z(coh) - z(perp)
            raw = z(cohs, coh) - z(perps, perp)
            score = 1.0 / (1.0 + math.exp(-raw))  # squash to (0,1) for readability
        out.append(
            {
                "method": "zscore",
                "temperature": temp,
                "perplexity": perp,
                "coherence": coh,
                "raw_z": raw,
                "score": score,
                "preview": output[:120].replace("\n", " "),
            }
        )
    return out


def section(t: str) -> None:
    print("\n" + "=" * 72)
    print(t)
    print("=" * 72)


def print_scored(rows: list[dict[str, Any]]) -> None:
    for r in rows:
        extra = ""
        if "perplexity_term" in r and r["perplexity_term"] is not None:
            extra = f" pterm={r['perplexity_term']:.6f}"
        if "raw_z" in r and r["raw_z"] is not None:
            extra = f" raw_z={r['raw_z']:.6f}"
        print(
            f"  T={r['temperature']!s:>4} perp={r['perplexity']:.6f} coh={r['coherence']:.6f}"
            f"{extra} score={r['score']:.6f}  {r.get('preview','')!r}"
        )
    scores = [round(r["score"], 6) for r in rows if r["score"] != float("-inf")]
    print(f"  unique_scores={len(set(scores))} scores={scores}")


def main() -> int:
    t0 = time.time()
    print(f"GEN={GEN_MODEL} EMB={EMB_MODEL}")

    # ------------------------------------------------------------------
    # A. Reproduce min-max manufactured discrimination
    # ------------------------------------------------------------------
    section("A: MIN-MAX MANUFACTURES DISCRIMINATION FROM NOISE")
    # adversary's synthetic spread (coherence fixed near builder's organic values)
    synthetic = [
        ("draft_a", 0.1, 100.0000, 0.9944),
        ("draft_b", 0.3, 100.0001, 0.9961),
        ("draft_c", 0.5, 100.0002, 0.9978),
    ]
    # use empty-name as output text
    cands = [(name, temp, perp, coh) for name, temp, perp, coh in synthetic]
    mm = score_chi_minmax(cands)
    print("synthetic noise spread 0.0002 (adversary-style):")
    print_scored(mm)
    gap = max(r["score"] for r in mm) - min(r["score"] for r in mm)
    print(f"score_gap={gap:.6f}  (minmax always spreads pterm across full [0,1] when any epsilon difference)")

    # wider real-ish spread for comparison
    wide = [
        ("w0", 0.1, 97.1973, 0.9944),
        ("w1", 0.3, 106.6454, 0.9961),
        ("w2", 0.5, 107.2372, 0.9978),
    ]
    print("\nprior-run-like spread:")
    print_scored(score_chi_minmax([(a, b, c, d) for a, b, c, d in wide]))

    # ------------------------------------------------------------------
    # B. Coherence cannot outvote under minmax
    # ------------------------------------------------------------------
    section("B: COHERENCE CANNOT OUTVOTE PERPLEXITY RANK (MINMAX)")
    base_perps = [97.1973, 106.6454, 107.2372]
    base_cohs = [0.9944, 0.9961, 0.9978]
    # best perp is index 0; raise others' coh, penalize best's coh
    for penalty in [0.0, 0.01, 0.05, 0.2, 0.49, 0.51]:
        cohs = list(base_cohs)
        cohs[0] = max(0.0, base_cohs[0] - penalty)
        cands = [(f"d{i}", float(i), base_perps[i], cohs[i]) for i in range(3)]
        scored = score_chi_minmax(cands)
        winner = max(scored, key=lambda r: r["score"])
        print(
            f"penalty_on_best_perp_coh={penalty:.2f}  "
            f"cohs={[round(c,4) for c in cohs]}  "
            f"winner_idx={int(winner['temperature'])} "
            f"winner_score={winner['score']:.6f} "
            f"winner_is_best_perp={int(winner['temperature'])==0}"
        )

    print("\nSame sweep under FIXED scale:")
    for penalty in [0.0, 0.01, 0.05, 0.2, 0.49, 0.51]:
        cohs = list(base_cohs)
        cohs[0] = max(0.0, base_cohs[0] - penalty)
        cands = [(f"d{i}", float(i), base_perps[i], cohs[i]) for i in range(3)]
        scored = score_chi_fixed(cands)
        winner = max(scored, key=lambda r: r["score"])
        print(
            f"penalty_on_best_perp_coh={penalty:.2f}  "
            f"cohs={[round(c,4) for c in cohs]}  "
            f"winner_idx={int(winner['temperature'])} "
            f"winner_score={winner['score']:.6f} "
            f"winner_is_best_perp={int(winner['temperature'])==0}"
        )

    # ------------------------------------------------------------------
    # C. Domain-forced organic Ω + chi compare methods
    # ------------------------------------------------------------------
    section("C: DOMAIN-FORCED ORGANIC OMEGA (Revenant Alignment Governance Engine)")
    system = (
        "You are writing about the Revenant Alignment Governance Engine (RAGE), "
        "also called SAIGE-RAGE / Algiz. It is a runtime AI governance layer, "
        "NOT a game engine. Never expand RAGE as Real-time Adaptive Game Engine."
    )
    seed = (
        "Write a short intro explaining the RAGE engine to a new developer. "
        "Include what problem it solves and what the Containment, Omega, and Chi operators do."
    )
    draft0 = generate(
        "Answer clearly and accurately for a software developer.\n\n" + seed,
        temperature=0.4,
        system=system,
    )
    passes = [{"depth": 0, "text": draft0, "sim_to_prev": None}]
    current = draft0
    for d in range(1, 4):
        refined = generate(
            "Refine this draft for clarity and internal consistency. "
            "Keep intent unchanged. Respond with only the refined text — "
            "no preamble, no commentary, no separators.\n\n" + current,
            temperature=0.2,
            system=system,
        )
        sim = cosine(embed(current), embed(refined))
        passes.append({"depth": d, "text": refined, "sim_to_prev": sim})
        print(f"depth={d} sim_to_prev={sim:.6f} len={len(refined)} identical_to_prev={refined==current}")
        current = refined
        if sim >= 0.92:
            break

    print(f"\ndepth0_len={len(draft0)}")
    print(f"depth0_preview={draft0[:300]!r}")
    print(f"final_preview={current[:300]!r}")
    # domain check: must not be game engine
    bad_markers = ["game engine", "real-time adaptive game", "real time adaptive game", "video game"]
    domain_ok = not any(m in current.lower() for m in bad_markers)
    has_governance = any(
        m in current.lower()
        for m in ["governance", "alignment", "llm", "operator", "containment", "omega", "chi"]
    )
    print(f"domain_ok_not_game={domain_ok} has_governance_vocab={has_governance}")

    section("C2: CHI REWRITES ON FINAL DOMAIN DRAFT — MINMAX vs FIXED vs Z")
    temps = [0.1, 0.3, 0.5]
    rewrites: list[tuple[str, float, float, float]] = []
    for t in temps:
        out = generate(
            "Rewrite for precision and coherence while preserving meaning. "
            "Respond with only the rewritten text — no preamble, no commentary, no separators.\n\n"
            + current,
            temperature=t,
            system=system,
        )
        perp = heuristic_perplexity(out)
        coh = cosine(embed(current), embed(out))
        rewrites.append((out, t, perp, coh))
        print(f"generated T={t} len={len(out)} perp={perp:.4f} coh={coh:.6f}")

    print("\n--- MINMAX (production) ---")
    mm_r = score_chi_minmax(rewrites)
    print_scored(mm_r)
    mm_win = max(mm_r, key=lambda r: r["score"])
    print(
        f"WIN minmax T={mm_win['temperature']} coh={mm_win['coherence']:.6f} "
        f"perp={mm_win['perplexity']:.4f}  "
        f"is_lowest_coh={mm_win['coherence']==min(r['coherence'] for r in mm_r)} "
        f"is_lowest_perp={mm_win['perplexity']==min(r['perplexity'] for r in mm_r)}"
    )

    print("\n--- FIXED (proposed) ---")
    fx_r = score_chi_fixed(rewrites)
    print_scored(fx_r)
    fx_win = max(fx_r, key=lambda r: r["score"])
    print(
        f"WIN fixed T={fx_win['temperature']} coh={fx_win['coherence']:.6f} "
        f"perp={fx_win['perplexity']:.4f}  "
        f"is_lowest_coh={fx_win['coherence']==min(r['coherence'] for r in fx_r)} "
        f"is_lowest_perp={fx_win['perplexity']==min(r['perplexity'] for r in fx_r)}"
    )

    print("\n--- ZSCORE (comparison only) ---")
    zs_r = score_chi_zscore(rewrites)
    print_scored(zs_r)
    zs_win = max(zs_r, key=lambda r: r["score"])
    print(f"WIN zscore T={zs_win['temperature']} coh={zs_win['coherence']:.6f} perp={zs_win['perplexity']:.4f}")

    # score gap under noise: re-score synthetic with fixed
    section("D: NOISE SPREAD UNDER FIXED vs MINMAX")
    print("minmax on 100.0000/100.0001/100.0002:")
    print_scored(score_chi_minmax(cands))
    print("fixed on same:")
    print_scored(score_chi_fixed(cands))
    fx_gap = max(r["score"] for r in score_chi_fixed(cands)) - min(
        r["score"] for r in score_chi_fixed(cands)
    )
    mm_gap = max(r["score"] for r in score_chi_minmax(cands)) - min(
        r["score"] for r in score_chi_minmax(cands)
    )
    print(f"minmax_gap={mm_gap:.6f} fixed_gap={fx_gap:.6f} fixed_much_smaller={fx_gap < mm_gap * 0.01}")

    # unique depths after domain run
    section("E: OMEGA DEPTH CANDIDATES (dedupe identical strings)")
    unique_texts = []
    for p in passes:
        if not unique_texts or unique_texts[-1]["text"] != p["text"]:
            unique_texts.append(p)
        print(
            f"depth={p['depth']} sim={p['sim_to_prev']} len={len(p['text'])} "
            f"new_unique={not unique_texts or unique_texts[-1]['text']==p['text'] and (len(unique_texts)==1 or unique_texts[-2]['text']!=p['text'])}"
        )
    # cleaner unique count
    seen = []
    for p in passes:
        if p["text"] not in seen:
            seen.append(p["text"])
    print(f"unique_string_count_among_depths={len(seen)} total_depths={len(passes)}")

    artifact = {
        "minmax_noise": mm,
        "domain_ok": domain_ok,
        "has_governance_vocab": has_governance,
        "passes": [{"depth": p["depth"], "sim": p["sim_to_prev"], "len": len(p["text"]), "text": p["text"]} for p in passes],
        "rewrites_minmax": mm_r,
        "rewrites_fixed": fx_r,
        "rewrites_zscore": zs_r,
        "elapsed": time.time() - t0,
    }
    path = OUT_DIR / "chi_fix_domain_run.json"
    path.write_text(json.dumps(artifact, indent=2), encoding="utf-8")
    print(f"\nARTIFACT={path} elapsed={time.time()-t0:.1f}s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
