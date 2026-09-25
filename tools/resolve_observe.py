#!/usr/bin/env python3
"""Run the observer-mode name resolution over the whole language corpus.

Every fixture under tests/integration/fixtures, every example and every oracle
probe goes through ``pymcu build`` with ``PYMCU_RESOLVE_OBSERVE=1``. The pass
decides each simple name's binding once, before lowering, derives the storage
key from that binding plus the expansion it is read in, and compares it with the
key the existing ladder produced. It writes nothing the compiler reads, so the
firmware is byte-identical to a build without the flag; the output is the list of
places where the two answers differ.

Each discrepancy line names the site (which of the ladder's qualifiers took the
decision), the file and line, the source name, both keys and the spelling class
of each.

  .venv/bin/python tools/resolve_observe.py              # print the grouped list
  .venv/bin/python tools/resolve_observe.py --json out.json
  .venv/bin/python tools/resolve_observe.py --jobs 4
"""
from __future__ import annotations

import argparse
import collections
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

LINE = re.compile(
    r"\[resolve-observe\] \[(?P<site>[^\]]+)\] (?P<file>[^:]*):(?P<line>-?\d+): "
    r"'(?P<name>[^']*)' ladder=(?P<ladder>\S+) \((?P<ladder_spelling>[^)]*)\) "
    r"resolution=(?P<mine>\S+) \((?P<mine_spelling>[^)]*)\) "
    r"scope=fn:(?P<fn>\S+) mod:(?P<mod>\S+) inline:(?P<inline>\S+)")
TOTALS = re.compile(r"\[resolve-observe\] \[totals\] (?P<body>.*)$")


def find_pymcu(arg: str | None) -> Path:
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


def corpus() -> list[tuple[str, Path]]:
    out: list[tuple[str, Path]] = []
    for base in (REPO / "tests/integration/fixtures", REPO / "examples"):
        if not base.is_dir():
            continue
        for d in sorted(base.iterdir()):
            if d.is_dir() and (d / "pyproject.toml").exists():
                out.append((d.name, d))
    return out


def probes() -> list[Path]:
    p = REPO / "tests/oracle/probes"
    return sorted(p.glob("*.py")) if p.is_dir() else []


def build(project: Path, pymcu: Path) -> tuple[int, str]:
    env = dict(os.environ)
    env["PYMCU_RESOLVE_OBSERVE"] = "1"
    env["COLUMNS"] = "400"
    try:
        r = subprocess.run([str(pymcu), "build"], cwd=project, capture_output=True,
                           text=True, env=env, timeout=300)
        return r.returncode, r.stdout + r.stderr
    except subprocess.TimeoutExpired:
        return -1, "<timeout>"


def run_project(name: str, project: Path, pymcu: Path) -> dict:
    with tempfile.TemporaryDirectory() as td:
        dst = Path(td) / name
        shutil.copytree(project, dst, ignore=shutil.ignore_patterns("dist", "build", ".venv"))
        rc, log = build(dst, pymcu)
    return parse(name, rc, log)


def run_probe(probe: Path, pymcu: Path) -> dict:
    with tempfile.TemporaryDirectory() as td:
        dst = Path(td) / probe.stem
        (dst / "src").mkdir(parents=True)
        (dst / "src" / "main.py").write_text(probe.read_text())
        (dst / "pyproject.toml").write_text(
            '[project]\nname = "oracle-probe"\nversion = "0.1.0"\n'
            'requires-python = ">=3.11"\n\n[tool.pymcu]\ntarget = "atmega328p"\n'
            'frequency = 16000000\nsources = "src"\nentry = "main.py"\n')
        rc, log = build(dst, pymcu)
    return parse(probe.stem, rc, log)


def parse(name: str, rc: int, log: str) -> dict:
    rows = [m.groupdict() for m in LINE.finditer(log)]
    totals = {}
    for m in TOTALS.finditer(log):
        for part in m.group("body").split():
            if "=" in part:
                k, v = part.split("=", 1)
                totals[k] = totals.get(k, 0) + int(v) if v.isdigit() else v
    return {"program": name, "rc": rc, "rows": rows, "totals": totals}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--pymcu")
    ap.add_argument("--jobs", type=int, default=4)
    ap.add_argument("--json")
    ap.add_argument("--filter", default="")
    args = ap.parse_args()
    pymcu = find_pymcu(args.pymcu)

    jobs: list = []
    for name, project in corpus():
        if args.filter and args.filter not in name:
            continue
        jobs.append(("project", name, project))
    for probe in probes():
        if args.filter and args.filter not in probe.stem:
            continue
        jobs.append(("probe", probe.stem, probe))

    results = []
    with ThreadPoolExecutor(max_workers=args.jobs) as ex:
        futures = [
            ex.submit(run_project, name, path, pymcu) if kind == "project"
            else ex.submit(run_probe, path, pymcu)
            for kind, name, path in jobs
        ]
        for i, f in enumerate(futures, 1):
            results.append(f.result())
            if i % 50 == 0:
                print(f"  {i}/{len(futures)}", file=sys.stderr, flush=True)

    all_rows = [dict(r, program=res["program"]) for res in results for r in res["rows"]]
    by_site = collections.Counter(r["site"] for r in all_rows)
    by_pair = collections.Counter(
        (r["site"], r["ladder_spelling"], r["mine_spelling"]) for r in all_rows)
    observed = sum(int(res["totals"].get("observed", 0) or 0) for res in results)
    agreed = sum(int(res["totals"].get("agreed", 0) or 0) for res in results)
    unclaimed = sum(int(res["totals"].get("unclaimed", 0) or 0) for res in results)
    discrepancies = sum(int(res["totals"].get("discrepancy", 0) or 0) for res in results)
    failed = [res["program"] for res in results if res["rc"] not in (0,)]

    print(f"programs                 {len(results)}")
    print(f"programs that refused    {len(failed)}")
    print(f"decisions observed       {observed}")
    print(f"  agreed                 {agreed}")
    print(f"  unclaimed by the pass  {unclaimed}")
    print(f"  DISCREPANT             {discrepancies}")
    print(f"distinct discrepancies   {len(all_rows)}")
    print()
    print("by site:")
    for site, n in by_site.most_common():
        print(f"  {site:26s} {n}")
    print()
    print("by (site, ladder spelling -> resolution spelling):")
    for (site, a, b), n in by_pair.most_common():
        print(f"  {n:5d}  {site:26s} {a} -> {b}")

    if args.json:
        Path(args.json).write_text(json.dumps(
            {"rows": all_rows,
             "totals": {"programs": len(results), "observed": observed,
                        "agreed": agreed, "unclaimed": unclaimed,
                        "discrepancies": discrepancies, "failed": failed}},
            indent=1))
        print(f"\nwrote {args.json}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
