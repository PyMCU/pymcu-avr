#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
# analyze.py — aggregate survey.json across the corpus into the PGO
# opportunity-sizing numbers (A..E) and the corpus table.

import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent / "out"


def load_all():
    recs = json.loads((OUT / "manifest.json").read_text())
    for r in recs:
        sj = Path(r.get("dist", "")) / "survey.json"
        r["survey"] = json.loads(sj.read_text()) if sj.is_file() else None
        sj2 = Path(r.get("dist", "")) / "survey-stim.json"
        r["stim"] = json.loads(sj2.read_text()) if sj2.is_file() else None
    return [r for r in recs if r.get("survey")]


def main():
    recs = load_all()
    tot_cyc = sum(r["survey"]["cyclesExecuted"] for r in recs)
    tot_instr = sum(r["survey"]["instructionsExecuted"] for r in recs)
    tot_flash = sum(r["survey"]["flashBytes"] for r in recs)
    tot_prog = sum(r["survey"]["programBytesExVectors"] for r in recs)
    tot_wall = sum(r["survey"]["wallMs"] for r in recs)
    n = len(recs)

    def agg(key, sub=None):
        v = 0
        for r in recs:
            s = r["survey"]
            v += s.get(key, 0) if sub is None else s.get(key, {}).get(sub, 0)
        return v

    # buckets
    delay = agg("buckets", "delayCycles")
    uart = agg("buckets", "uartCycles")
    isr = agg("buckets", "isrCycles")
    user = agg("buckets", "userCycles")

    # branches
    taken = agg("branches", "takenExecs")
    condT = agg("branches", "condTotal")
    condE = agg("branches", "condExec")
    alwaysT = agg("branches", "alwaysTaken")
    neverT = agg("branches", "neverTaken")
    mixed = agg("branches", "mixed")
    neverX = agg("branches", "neverExec")

    # cold
    cold = agg("cold", "bytes")
    coldOutline = agg("cold", "inOutlineBytes")
    coldExec = agg("cold", "inExecutedFuncBytes")
    dataB = agg("cold", "dataBytes")
    neverEntered = sum(len(r["survey"]["cold"]["neverEntered"]) for r in recs)

    # hot-loop memory traffic
    hotLdsSts = agg("hot", "ldsStsExecs") + agg("hot", "ldStExecs") \
        + agg("hot", "pushPopExecs")
    hotMemCyc = agg("hot", "memOpCycles")
    hotMov = agg("hot", "movExecs")
    sramVars = sum(len(r["survey"]["hot"]["distinctSramAddrs"]) for r in recs)

    # loops
    loops = sum(len(r["survey"]["loops"]) for r in recs)
    smallConst = sum(len(r["survey"]["loopOpportunity"]["smallConstTrips"])
                     for r in recs)
    unrolled = sum(len(r["survey"]["loopOpportunity"]["unrolledRare"])
                   for r in recs)

    # stalls
    stalled = [r for r in recs if r["survey"]["stall"]["stalled"]]
    stim_cleared = [r for r in stalled if r.get("stim")
                    and not r["stim"]["stall"]["stalled"]]

    print(f"corpus: {n} programs, {tot_flash} flash bytes "
          f"({tot_prog} ex-vectors), {tot_cyc/1e9:.2f}G cycles simulated, "
          f"{tot_instr/1e9:.2f}G instrs, wall {tot_wall/1000:.0f}s")
    print(f"buckets: delay={delay/tot_cyc:.1%} uart={uart/tot_cyc:.1%} "
          f"isr={isr/tot_cyc:.1%} user={user/tot_cyc:.1%}")
    print(f"branches: {condT} cond sites, {condE} executed, "
          f"alwaysT={alwaysT} neverT={neverT} mixed={mixed} neverX={neverX}; "
          f"taken execs={taken/1e6:.0f}M = {taken/tot_cyc:.1%} of cycles")
    print(f"cold: {cold} B never-executed ({cold/tot_prog:.1%} of program bytes); "
          f"{coldOutline} B inside __pymcu_outline_*, {coldExec} B inside "
          f"executed functions, {dataB} B flash data; {neverEntered} symbols "
          f"never entered")
    print(f"hot loops: {hotLdsSts/1e6:.1f}M mem-op execs "
          f"({hotMemCyc/1e6:.0f}M cyc = {hotMemCyc/tot_cyc:.2%} of all cycles), "
          f"{hotMov/1e6:.1f}M mov execs, {sramVars} distinct SRAM addrs")
    print(f"loops: {loops} back-edges; {smallConst} small-constant-trip; "
          f"{unrolled} unrolled-rare sequences")
    print(f"stalled: {len(stalled)} base; stimulus cleared "
          f"{len(stim_cleared)} of {sum(1 for r in stalled if r.get('stim'))}")

    # per-program table (markdown for the report)
    rows = sorted(recs, key=lambda r: -r["survey"]["cyclesExecuted"])
    with open(OUT / "corpus.md", "w") as f:
        f.write("| program | flash B | cycles | stall | delay% | uart% | isr% |"
                " user% | top3 | cold% | taken-br% | hot mem | sram vars |\n")
        f.write("|---|---|---|---|---|---|---|---|---|---|---|---|---|\n")
        for r in rows:
            s = r["survey"]
            st = s["stall"]["kind"] if s["stall"]["stalled"] else "-"
            tops = "; ".join(f"{t['name']}={t['share']*100:.0f}%"
                             for t in s["topSymbols"][:3])
            mem = (s["hot"]["ldsStsExecs"] + s["hot"]["ldStExecs"]
                   + s["hot"]["pushPopExecs"])
            f.write(f"| {r['name']} | {s['flashBytes']} | "
                    f"{s['cyclesExecuted']} | {st} | "
                    f"{s['buckets']['delayShare']*100:.0f} | "
                    f"{s['buckets']['uartShare']*100:.0f} | "
                    f"{s['buckets']['isrShare']*100:.0f} | "
                    f"{s['buckets']['userShare']*100:.0f} | {tops} | "
                    f"{s['cold']['share']*100:.0f} | "
                    f"{s['branches']['takenCycleShare']*100:.0f} | "
                    f"{mem} | {len(s['hot']['distinctSramAddrs'])} |\n")
    print(f"wrote {OUT/'corpus.md'}")

    # delay-share leaders / cold leaders for the report
    by_delay = sorted(recs, key=lambda r: -r["survey"]["buckets"]["delayCycles"])
    print("\ntop delay-cycle programs:")
    for r in by_delay[:8]:
        s = r["survey"]
        print(f"  {r['name']:34s} delay={s['buckets']['delayCycles']/1e6:.2f}M "
              f"({s['buckets']['delayShare']*100:.0f}%)")
    by_cold = sorted(recs, key=lambda r: -r["survey"]["cold"]["bytes"])
    print("top cold-byte programs:")
    for r in by_cold[:8]:
        s = r["survey"]
        print(f"  {r['name']:34s} cold={s['cold']['bytes']}B "
              f"({s['cold']['share']*100:.0f}%) flash={s['flashBytes']}B")


if __name__ == "__main__":
    main()
