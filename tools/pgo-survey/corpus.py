#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# corpus.py, build + survey the whole ATmega328P corpus for the PGO study.
#
# Enumerates examples/* and tests/integration/fixtures/*, builds each project
# with the worktree's own pymcuc-avr (PYMCU_BACKEND_BINARY), extracts ELF
# symbols with avr-nm (the same override profile.py performs, backend
# --emit-symbols counts IR instructions, not assembled words), runs the
# pgo-survey collector for a fixed simulated window, and aggregates CSV.
#
# Usage:
#   corpus.py build            # build all eligible projects (parallel)
#   corpus.py survey           # run the survey on every built dist (parallel)
#   corpus.py stim             # rerun stalled programs with generic stimulus
#   corpus.py aggregate        # collect survey.json rows -> out/survey.csv
#   corpus.py bench <name>     # emulator-cost benchmark on one program
#
# All corpus output lands in tools/pgo-survey/out/ (gitignored).

import json
import os
import subprocess
import sys
import tomllib
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT = REPO / "tools" / "pgo-survey" / "out"
SURVEY_DLL = (REPO / "tools" / "pgo-survey" / "bin" / "Release" / "net10.0"
              / "pymcuc-avr-pgo-survey.dll")
PYMCU = REPO / ".venv" / "bin" / "pymcu"
BACKEND = REPO / "build" / "bin" / "pymcuc-avr"
AVR_NM = "/opt/homebrew/bin/avr-nm"

RUN_SECONDS = 0.200            # 200 simulated ms per program
WORKERS = min(8, os.cpu_count() or 4)
AVR_CLASS = {"atmega328p", "atmega328"}
# board -> chip, mirroring driver/core/boards.py BOARD_CHIPS (AVR family only)
BOARD_CHIPS = {"arduino_uno": "atmega328p", "arduino_nano": "atmega328p",
               "uno": "atmega328p"}


def projects():
    """Every example/fixture project with its parsed tool.pymcu config."""
    for base in (REPO / "examples", REPO / "tests" / "integration" / "fixtures"):
        for d in sorted(base.iterdir()):
            pp = d / "pyproject.toml"
            if not pp.is_file():
                continue
            cfg = tomllib.loads(pp.read_text()).get("tool", {}).get("pymcu", {})
            yield d, cfg


def build_one(d: Path, cfg: dict) -> dict:
    name = d.name
    chip = cfg.get("target") or cfg.get("chip") \
        or BOARD_CHIPS.get(cfg.get("board", ""), "")
    if chip not in AVR_CLASS:
        return {"name": name, "dir": str(d),
                "skipped": f"chip={chip or cfg.get('target') or cfg.get('board')}"}
    dist = d / "dist"
    if (dist / "debug" / "firmware.elf").is_file() \
            and (dist / "firmware.hex").is_file() \
            and (dist / "firmware.symbols.json").is_file():
        return {"name": name, "dir": str(d), "dist": str(dist),
                "freq": cfg.get("frequency", 16_000_000), "cached": True}
    env = dict(os.environ)
    env["PYMCU_BACKEND_BINARY"] = f"avr={BACKEND}"
    try:
        p = subprocess.run([str(PYMCU), "build"], cwd=d, env=env,
                           capture_output=True, text=True, timeout=180)
    except subprocess.TimeoutExpired:
        return {"name": name, "dir": str(d), "failed": "build timeout"}
    if p.returncode != 0:
        return {"name": name, "dir": str(d), "failed":
                (p.stderr or p.stdout).strip().splitlines()[-3:]}
    dist = d / "dist"
    elf = dist / "debug" / "firmware.elf"
    hex_f = dist / "firmware.hex"
    if not elf.is_file() or not hex_f.is_file():
        return {"name": name, "dir": str(d), "failed": "no elf/hex emitted"}
    nm = subprocess.run([AVR_NM, "--format=bsd", str(elf)],
                        capture_output=True, text=True)
    text, data = [], []
    for line in nm.stdout.splitlines():
        parts = line.split()
        if len(parts) < 3:
            continue
        addr, typ, sym = int(parts[0], 16), parts[1], parts[2]
        if typ in "tT":
            text.append({"Name": sym, "WordAddr": addr // 2})
        elif typ in "abd":
            data.append({"Name": sym, "WordAddr": addr})
    (dist / "firmware.symbols.json").write_text(
        json.dumps({"Symbols": text, "DataSymbols": data}))
    return {"name": name, "dir": str(d), "dist": str(dist),
            "freq": cfg.get("frequency", 16_000_000)}


def survey_one(rec: dict, stim: bool) -> dict:
    if "dist" not in rec:
        return rec
    cycles = int(rec["freq"] * RUN_SECONDS)
    cmd = ["dotnet", str(SURVEY_DLL), rec["dist"],
           "--cycles", str(cycles), "--freq", str(rec["freq"]),
           "--out", rec["dist"] + ("/survey-stim.json" if stim else "/survey.json")]
    if stim:
        cmd.append("--stim")
    try:
        p = subprocess.run(cmd, capture_output=True, text=True, timeout=300)
        rec["csv"] = p.stdout.strip().splitlines()[-1] if p.stdout.strip() else ""
        if p.returncode != 0:
            rec["failed"] = "survey: " + p.stderr.strip().splitlines()[-1]
    except subprocess.TimeoutExpired:
        rec["failed"] = "survey timeout"
    return rec


def phase(name, fn, items):
    results = []
    with ThreadPoolExecutor(max_workers=WORKERS) as ex:
        futs = {ex.submit(fn, it): it for it in items}
        done = 0
        for f in as_completed(futs):
            results.append(f.result())
            done += 1
            if done % 25 == 0 or done == len(items):
                print(f"[{name}] {done}/{len(items)}", file=sys.stderr, flush=True)
    return results


def load_manifest() -> list[dict]:
    mf = OUT / "manifest.json"
    return json.loads(mf.read_text()) if mf.is_file() else []


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "all"
    OUT.mkdir(parents=True, exist_ok=True)

    if cmd == "build":
        items = [(d, c) for d, c in projects()]
        recs = phase("build", lambda t: build_one(*t), items)
        (OUT / "manifest.json").write_text(json.dumps(recs, indent=1))
        ok = [r for r in recs if "dist" in r]
        sk = [r for r in recs if "skipped" in r]
        fa = [r for r in recs if "failed" in r]
        print(f"built={len(ok)} skipped={len(sk)} failed={len(fa)}")
        for r in sk: print(f"  skip  {r['name']}: {r['skipped']}")
        for r in fa: print(f"  FAIL  {r['name']}: {r['failed']}")

    elif cmd in ("survey", "stim", "aggregate", "bench"):
        recs = [r for r in load_manifest() if "dist" in r]
        if cmd == "survey":
            recs = phase("survey", lambda r: survey_one(r, False), recs)
            (OUT / "manifest.json").write_text(json.dumps(recs, indent=1))
        elif cmd == "stim":
            stalled = [r for r in recs
                       if (s := _stall(r)) and s.get("stalled")]
            print(f"stalled: {len(stalled)}")
            phase("stim", lambda r: survey_one(r, True), stalled)
        elif cmd == "aggregate":
            rows = ["name,flashBytes,cyclesExecuted,instructionsExecuted,"
                    "endReason,stall,delayShare,uartShare,isrShare,userShare,"
                    "topSymbols,coldShare,takenBranchShare,hotLdsStsExecs,"
                    "hotSramVars,wallMs"]
            for r in recs:
                sj = Path(r["dist"]) / "survey.json"
                rows.append(r.get("csv", ""))
            (OUT / "survey.csv").write_text("\n".join(rows) + "\n")
            print(f"wrote {OUT/'survey.csv'} ({len(rows)-1} rows)")
        elif cmd == "bench":
            name = sys.argv[2]
            r = next(r for r in recs if r["name"] == name)
            subprocess.run(["dotnet", str(SURVEY_DLL), r["dist"],
                            "--bench", "--cycles", str(int(r["freq"] * 0.2)),
                            "--freq", str(r["freq"])])


def _stall(rec):
    p = Path(rec.get("dist", "")) / "survey.json"
    return json.loads(p.read_text())["stall"] if p.is_file() else None


if __name__ == "__main__":
    main()
