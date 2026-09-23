#!/usr/bin/env python3
"""Run the IR verifier over the whole language corpus and ratchet the result.

Every fixture under tests/integration/fixtures, every example, and every oracle
probe goes through ``pymcu build`` with ``PYMCU_VERIFY_IR=1``. The full build is
deliberate: the driver stages generated modules (the ``board`` module, the
console shim, the arena preamble) that a bare ``pymcuc --emit-ir`` cannot see,
and a real part of the corpus does not compile without them.

The corpus already violates a handful of invariants on purpose: some probes
exist to pin down a known miscompile, and the verifier names it. A plain "any
warning fails" gate would be red forever, so the script diffs against
tools/verify_ir_baseline.json instead:

  REGRESSION  -- a violation signature the baseline does not list, or a program
                 that compiled in the baseline and fails now.
  CLEARED     -- a baseline violation gone from the run (good news; print it,
                 do not fail -- tighten the baseline with --write-baseline).

Run it before bisecting a miscompile: when the verifier already sees the
violation, the message names the guilty pass.

  .venv/bin/python tools/verify_ir.py                 # compare to baseline
  .venv/bin/python tools/verify_ir.py --write-baseline # regenerate baseline
  .venv/bin/python tools/verify_ir.py --jobs 4        # bound parallelism
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
BASELINE = Path(__file__).resolve().with_name("verify_ir_baseline.json")

WARN = re.compile(r"\[ir-verify\] \[([^\]]+)\] (\S+) (\S+): (.*)$", re.M)


def find_pymcu(arg: str | None) -> Path:
    """The ``pymcu`` driver binary: explicit choice, then $PYMCU_BIN, then the
    venv this script runs in, then PATH."""
    if arg:
        return Path(arg)
    env = os.environ.get("PYMCU_BIN")
    if env:
        return Path(env)
    venv = Path(sys.executable).parent / "pymcu"
    if venv.exists():
        return venv
    which = shutil.which("pymcu")
    if which:
        return Path(which)
    sys.exit("pymcu driver not found: pass --pymcu, set PYMCU_BIN, or run "
             "inside the repo .venv")


def build_project(project: Path, pymcu: Path) -> tuple[int, str]:
    env = dict(os.environ)
    env["PYMCU_VERIFY_IR"] = "1"
    env["COLUMNS"] = "400"
    try:
        r = subprocess.run([str(pymcu), "build"], cwd=project,
                           capture_output=True, text=True, env=env, timeout=300)
        return r.returncode, r.stdout + r.stderr
    except subprocess.TimeoutExpired:
        return -1, "<timeout>"


def run_one(name: str, project: Path, pymcu: Path) -> dict:
    rc, log = build_project(project, pymcu)
    violations = sorted(f"{p}|{c}|{w}|{d.strip()}"
                        for p, c, w, d in WARN.findall(log))
    return {"program": name, "rc": rc, "violations": violations}


def run_probe(probe: Path, project: Path, pymcu: Path) -> dict:
    """An oracle probe has no project of its own; wrap one around it the same
    way the sweep corpus does."""
    (project / "src").mkdir(parents=True, exist_ok=True)
    (project / "src" / "main.py").write_text(probe.read_text())
    (project / "pyproject.toml").write_text(
        '[project]\nname = "oracle-probe"\nversion = "0.1.0"\n'
        'requires-python = ">=3.11"\n\n[tool.pymcu]\ntarget = "atmega328p"\n'
        'frequency = 16000000\nsources = "src"\nentry = "main.py"\n')
    return run_one(probe.stem, project, pymcu)


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--write-baseline", action="store_true",
                    help="record the run as the new baseline instead of diffing")
    ap.add_argument("--jobs", type=int, default=max(1, (os.cpu_count() or 4) // 2))
    ap.add_argument("--pymcu", default=None,
                    help="pymcu driver binary (default: $PYMCU_BIN, the venv's, or PATH)")
    ap.add_argument("--only", nargs="*", default=None,
                    help="restrict to these program names (debugging)")
    ns = ap.parse_args()
    pymcu = find_pymcu(ns.pymcu)

    fixtures = [d for d in sorted((REPO / "tests/integration/fixtures").iterdir())
                if (d / "pyproject.toml").is_file()]
    examples = [d for d in sorted((REPO / "examples").iterdir())
                if (d / "pyproject.toml").is_file()]
    probes = sorted((REPO / "tests/oracle/probes").glob("*.py"))
    if ns.only:
        wanted = set(ns.only)
        fixtures = [d for d in fixtures if d.name in wanted]
        examples = [d for d in examples if d.name in wanted]
        probes = [p for p in probes if p.stem in wanted]

    results: dict[str, dict] = {}
    total = len(fixtures) + len(examples) + len(probes)
    with tempfile.TemporaryDirectory(prefix="verify-ir-") as td:
        tmpdir = Path(td)
        with ThreadPoolExecutor(max_workers=ns.jobs) as pool:
            futs = [pool.submit(run_one, d.name, d, pymcu)
                    for d in fixtures + examples]
            for p in probes:
                proj = tmpdir / p.stem
                futs.append(pool.submit(run_probe, p, proj, pymcu))
            done = 0
            for fut in futs:
                r = fut.result()
                results[r["program"]] = {"rc": r["rc"],
                                         "violations": r["violations"]}
                done += 1
                if done % 100 == 0:
                    print(f"{done}/{total}", file=sys.stderr, flush=True)

    if ns.write_baseline:
        clean = {n: {"rc": r["rc"], "violations": r["violations"]}
                 for n, r in results.items()}
        BASELINE.write_text(json.dumps(
            {"version": 1, "programs": clean}, indent=1, sort_keys=True) + "\n")
        print(f"wrote {BASELINE} ({len(results)} programs)")
        return 0

    if not BASELINE.exists():
        sys.exit(f"no baseline at {BASELINE}; run with --write-baseline first")
    base = json.loads(BASELINE.read_text())["programs"]

    regressions, cleared = [], []
    for name, r in sorted(results.items()):
        b = base.get(name)
        if r["rc"] != 0 and (b is None or b["rc"] == 0):
            regressions.append(f"{name}: compile now fails (rc={r['rc']})")
        old = set(b["violations"]) if b else set()
        new = set(r["violations"])
        for v in sorted(new - old):
            regressions.append(f"{name}: +{v}")
        for v in sorted(old - new):
            cleared.append(f"{name}: -{v}")
    for name in sorted(set(base) - set(results)):
        cleared.append(f"{name}: program left the corpus")

    n_viol = sum(len(r["violations"]) for r in results.values())
    n_bad = sum(1 for r in results.values() if r["rc"] != 0)
    print(f"verify-ir: {len(results)} programs, {n_viol} violation reports, "
          f"{n_bad} compile failures")
    for c in cleared:
        print(f"  cleared {c}")
    if regressions:
        print(f"  {len(regressions)} regressions vs baseline:")
        for r_ in regressions:
            print(f"  REGRESSED {r_}")
        return 1
    print("  no regressions vs baseline")
    return 0


if __name__ == "__main__":
    sys.exit(main())
